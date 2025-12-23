using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.Statistics;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Coordinator.Rounds;

public partial class Arena : PeriodicRunner
{
	public Arena(
		WabiSabiConfig config,
		IRPCClient rpc,
		Prison prison,
		RoundParameterFactory roundParametersFactory,
		FeeRateProvider feeRateProvider,
		CoinJoinScriptStore? coinJoinScriptStore = null,
		TimeSpan? period = null
		) : base(period ?? TimeSpan.FromSeconds(2))
	{
		_config = config;
		_rpc = rpc;
		_prison = prison;
		_coinJoinScriptStore = coinJoinScriptStore;
		_roundParametersFactory = roundParametersFactory;
		_feeRateProvider = feeRateProvider;
		_maxSuggestedAmountProvider = new(_config);
	}

	public HashSet<Round> Rounds { get; } = new();
	private ImmutableList<RoundState> _roundStates = ImmutableList<RoundState>.Empty;
	private readonly ConcurrentQueue<uint256> _disruptedRounds = new();
	private readonly AsyncLock _asyncLock = new();
	private readonly WabiSabiConfig _config;
	private readonly IRPCClient _rpc;
	private readonly Prison _prison;
	private readonly CoinJoinScriptStore? _coinJoinScriptStore;
	private readonly RoundParameterFactory _roundParametersFactory;
	private readonly FeeRateProvider _feeRateProvider;
	private readonly MaxSuggestedAmountProvider _maxSuggestedAmountProvider;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		using (await _asyncLock.LockAsync(cancel).ConfigureAwait(false))
		{
			TimeoutRounds();

			TimeoutAlices();

			await StepTransactionSigningPhaseAsync(cancel).ConfigureAwait(false);

			StepOutputRegistrationPhase();

			await StepConnectionConfirmationPhaseAsync(cancel).ConfigureAwait(false);

			await StepInputRegistrationPhaseAsync(cancel).ConfigureAwait(false);

			cancel.ThrowIfCancellationRequested();

			// Ensure there's at least one non-blame round in input registration.
			await CreateRoundsAsync(cancel).ConfigureAwait(false);

			AbortDisruptedRounds();

			// RoundStates have to contain all states. Do not change stateId=0.
			SetRoundStates();

		}
	}

	private void SetRoundStates()
	{
		// Order rounds ascending by max suggested amount, then ascending by input count.
		// This will make sure WW2.0.1 clients register according to our desired order.
		var rounds = Rounds
						.OrderBy(x => x.Parameters.MaxSuggestedAmount)
						.ThenBy(x => x.InputCount)
						.ToList();

		_roundStates = rounds.Select(r => RoundState.FromRound(r, stateId: 0)).ToImmutableList();
	}

	private async Task StepInputRegistrationPhaseAsync(CancellationToken cancel)
	{
		foreach (var round in Rounds.Where(x =>
			x.Phase == Phase.InputRegistration
			&& x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound))
			.ToArray())
		{
			try
			{
				await foreach (var offendingAlices in CheckTxoSpendStatusAsync(round, cancel).ConfigureAwait(false))
				{
					if (offendingAlices.Length != 0)
					{
						round.Alices.RemoveAll(x => offendingAlices.Contains(x));
					}
				}

				if (round.InputCount < round.Parameters.MinInputCountByRound)
				{
					if (!round.InputRegistrationTimeFrame.HasExpired)
					{
						continue;
					}

					_maxSuggestedAmountProvider.StepMaxSuggested(round, false);
					EndRound(round, EndRoundState.AbortedNotEnoughAlices);
					Logger.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.InputRegistration)} phase. The minimum is ({round.Parameters.MinInputCountByRound}). {nameof(round.Parameters.MaxSuggestedAmount)} was '{round.Parameters.MaxSuggestedAmount}' BTC.", round);
				}
				else if (round.IsInputRegistrationEnded(round.Parameters.MaxInputCountByRound))
				{
					_maxSuggestedAmountProvider.StepMaxSuggested(round, true);
					SetRoundPhase(round, Phase.ConnectionConfirmation);
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
				Logger.LogError(ex.Message, round);
			}
		}
	}

	private async Task StepConnectionConfirmationPhaseAsync(CancellationToken cancel)
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.ConnectionConfirmation).ToArray())
		{
			try
			{
				if (round.Alices.All(x => x.ConfirmedConnection))
				{
					SetRoundPhase(round, Phase.OutputRegistration);
				}
				else if (round.ConnectionConfirmationTimeFrame.HasExpired)
				{
					var alicesDidNotConfirm = round.Alices.Where(x => !x.ConfirmedConnection).ToArray();
					if (ReasonableOffendersCount(alicesDidNotConfirm.Length, round.Parameters.MinInputCountByRound))
					{
						foreach (var alice in alicesDidNotConfirm)
						{
							_prison.FailedToConfirm(alice.Coin.Outpoint, alice.Coin.Amount, round.Id);
						}
					}
					else
					{
						Logger.LogWarning($"{round.Id}: Tried to ban {alicesDidNotConfirm.Length} inputs for FailedToConfirm - ban was skipped.");
						foreach (var alice in alicesDidNotConfirm)
						{
							_prison.CoordinatorStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
						}
					}
					var removedAliceCount = round.Alices.RemoveAll(x => alicesDidNotConfirm.Contains(x));
					Logger.LogInfo($"{removedAliceCount} alices removed because they didn't confirm.", round);

					// Once an input is confirmed and non-zero credentials are issued, it is too late to do any
					if (round.InputCount >= round.Parameters.MinInputCountByRound)
					{
						var allOffendingAlices = new List<Alice>();
						await foreach (var offendingAlices in CheckTxoSpendStatusAsync(round, cancel).ConfigureAwait(false))
						{
							allOffendingAlices.AddRange(offendingAlices);
						}

						if (ReasonableOffendersCount(allOffendingAlices.Count, round.Parameters.MinInputCountByRound))
						{
							foreach (var offender in allOffendingAlices)
							{
								_prison.DoubleSpent(offender.Coin.Outpoint, offender.Coin.Amount, round.Id);
							}
						}
						else
						{
							Logger.LogWarning($"{round.Id}: Tried to ban {allOffendingAlices.Count} inputs for FailedToConfirm - ban was skipped.");
							foreach (var alice in allOffendingAlices)
							{
								_prison.CoordinatorStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
							}
						}
						if (allOffendingAlices.Count > 0)
						{
							Logger.LogInfo($"There were {allOffendingAlices.Count} alices that spent the registered UTXO. Aborting...", round);

							await EndRoundAndTryCreateBlameRoundAsync(round, cancel).ConfigureAwait(false);
							return;
						}
					}

					if (round.InputCount < round.Parameters.MinInputCountByRound)
					{
						EndRound(round, EndRoundState.AbortedNotEnoughAlices);
						Logger.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.ConnectionConfirmation)} phase. The minimum is ({round.Parameters.MinInputCountByRound}).", round);
					}
					else
					{
						round.OutputRegistrationTimeFrame = TimeFrame.Create(_config.FailFastOutputRegistrationTimeout);
						SetRoundPhase(round, Phase.OutputRegistration);
					}
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
				Logger.LogError(ex.Message, round);
			}
		}
	}

	private void StepOutputRegistrationPhase()
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.OutputRegistration).ToArray())
		{
			try
			{
				var allReady = round.Alices.All(a => a.ReadyToSign);
				bool phaseExpired = round.OutputRegistrationTimeFrame.HasExpired;

				if (allReady || phaseExpired)
				{
					var coinjoin = round.Assert<ConstructionState>();

					Logger.LogInfo($"{coinjoin.Inputs.Count()} inputs were added.", round);
					Logger.LogInfo($"{coinjoin.Outputs.Count()} outputs were added.", round);

					round.CoordinatorScript = GetCoordinatorScriptPreventReuse(round);
					coinjoin = AddCoordinationFee(round, coinjoin, round.CoordinatorScript);

					round.CoinjoinState = FinalizeTransaction(coinjoin);

					if (!allReady && phaseExpired)
					{
						// It would be better to end the round and create a blame round here, but older client would not support it.
						// See https://github.com/WalletWasabi/WalletWasabi/pull/11028.
						round.TransactionSigningTimeFrame = TimeFrame.Create(_config.FailFastTransactionSigningTimeout);
						round.FastSigningPhase = true;
					}

					SetRoundPhase(round, Phase.TransactionSigning);
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
				Logger.LogError(ex.Message, round);
			}
		}
	}

	private async Task StepTransactionSigningPhaseAsync(CancellationToken cancellationToken)
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.TransactionSigning).ToArray())
		{
			var state = round.Assert<SigningState>();

			try
			{
				if (state.IsFullySigned)
				{
					Transaction coinjoin = state.CreateTransaction();

					// Logging.
					Logger.LogInfo("Trying to broadcast coinjoin.", round);
					Coin[] spentCoins = round.CoinjoinState.Inputs.ToArray();
					Money networkFee = coinjoin.GetFee(spentCoins);
					Logger.LogInfo($"Network Fee: {networkFee.ToString(false, false)} BTC.", round);
					FeeRate feeRate = coinjoin.GetFeeRate(spentCoins);
					Logger.LogInfo($"Network Fee Rate: {feeRate.SatoshiPerByte} sat/vByte.", round);
					Logger.LogInfo($"Desired Fee Rate: {round.Parameters.MiningFeeRate.SatoshiPerByte} sat/vByte.", round);

					// Added for monitoring reasons.
					try
					{
						var targetFeeRate = await GetFeeRateEstimationAsync(cancellationToken).ConfigureAwait(false);
						Logger.LogInfo($"Current Fee Rate on the Network: {targetFeeRate.SatoshiPerByte} sat/vByte. Confirmation target is: {(int)_config.ConfirmationTarget} blocks.", round);
					}
					catch (Exception ex)
					{
						Logger.LogDebug($"Could not log fee rate monitoring: '{ex.Message}'.", round);
					}

					Logger.LogInfo($"Number of inputs: {coinjoin.Inputs.Count}.", round);
					Logger.LogInfo($"Number of outputs: {coinjoin.Outputs.Count}.", round);
					Logger.LogInfo($"Serialized Size: {coinjoin.GetSerializedSize() / 1024.0} KB.", round);
					Logger.LogInfo($"VSize: {coinjoin.GetVirtualSize() / 1024.0} KB.", round);
					var indistinguishableOutputs = coinjoin.GetIndistinguishableOutputs(includeSingle: true);
					foreach (var (value, count) in indistinguishableOutputs.Where(x => x.count > 1))
					{
						Logger.LogInfo($"There are {count} occurrences of {value.ToString(true, false)} outputs.", round);
					}

					Logger.LogInfo(
						$"There are {indistinguishableOutputs.Count(x => x.count == 1)} occurrences of unique outputs.", round);

					// Broadcasting.
					await _rpc.SendRawTransactionAsync(coinjoin, cancellationToken).ConfigureAwait(false);
					EndRound(round, EndRoundState.TransactionBroadcasted);
					Logger.LogInfo($"Successfully broadcast the coinjoin: {coinjoin.GetHash()}.", round);

					var coordinatorScriptPubKey = _config.GetNextCleanCoordinatorScript();
					if (round.CoordinatorScript == coordinatorScriptPubKey)
					{
						_config.MakeNextCoordinatorScriptDirty();
					}

					foreach (var address in coinjoin.Outputs
						.Select(x => x.ScriptPubKey)
						.Where(script => _coinJoinScriptStore?.Contains(script) is true))
					{
						if (address == round.CoordinatorScript)
						{
							Logger.LogError(
								$"Coordinator script pub key reuse detected: {round.CoordinatorScript.ToHex()}", round);
						}
						else
						{
							Logger.LogError($"Output script pub key reuse detected: {address.ToHex()}", round);
						}
					}

					_coinJoinScriptStore?.AddRange(coinjoin.Outputs.Select(x => x.ScriptPubKey));
				}
				else if (round.TransactionSigningTimeFrame.HasExpired)
				{
					Logger.LogWarning($"Signing phase failed with timed out after {round.TransactionSigningTimeFrame.Duration.TotalSeconds} seconds.", round);
					if (round.FastSigningPhase)
					{
						await FailFastTransactionSigningPhaseAsync(round, cancellationToken).ConfigureAwait(false);
					}
					else
					{
						await FailTransactionSigningPhaseAsync(round, cancellationToken).ConfigureAwait(false);
					}
				}
			}
			catch (RPCException ex)
			{
				Logger.LogError($"Transaction broadcasting failed: '{ex}'.", round);
				EndRound(round, EndRoundState.TransactionBroadcastFailed);
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"Signing phase failed, reason: '{ex}'.", round);
				EndRound(round, EndRoundState.AbortedWithError);
			}
		}
	}

	private async IAsyncEnumerable<Alice[]> CheckTxoSpendStatusAsync(Round round, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var chunkOfAlices in round.Alices.ToList().ChunkBy(16))
		{
			var batchedRpc = _rpc.PrepareBatch();

			var aliceCheckingTaskPairs = chunkOfAlices
				.Select(x => (Alice: x, StatusTask: _rpc.GetTxOutAsync(x.Coin.Outpoint.Hash, (int)x.Coin.Outpoint.N, includeMempool: true, cancellationToken)))
				.ToList();

			await batchedRpc.SendBatchAsync(cancellationToken).ConfigureAwait(false);

			var spendStatusCheckingTasks = aliceCheckingTaskPairs.Select(async x => (x.Alice, Status: await x.StatusTask.ConfigureAwait(false)));
			var alices = await Task.WhenAll(spendStatusCheckingTasks).ConfigureAwait(false);
			yield return alices.Where(x => x.Status is null).Select(x => x.Alice).ToArray();
		}
	}

	private async Task FailTransactionSigningPhaseAsync(Round round, CancellationToken cancellationToken)
	{
		var state = round.Assert<SigningState>();

		var unsignedOutpoints = state.UnsignedInputs.Select(c => c.Outpoint).ToHashSet();

		var alicesWhoDidNotSign = round.Alices
			.Where(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint))
			.ToHashSet();

		if (ReasonableOffendersCount(alicesWhoDidNotSign.Count, round.Parameters.MinInputCountByRound))
		{
			foreach (var alice in alicesWhoDidNotSign)
			{
				_prison.FailedToSign(alice.Coin.Outpoint, alice.Coin.Amount, round.Id);
			}
		}
		else
		{
			Logger.LogWarning($"{round.Id}: Tried to ban {alicesWhoDidNotSign.Count} inputs for FailedToConfirm - ban was skipped.");
			foreach (var alice in alicesWhoDidNotSign)
			{
				_prison.CoordinatorStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
			}
		}

		var cnt = round.Alices.RemoveAll(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint));

		Logger.LogInfo($"Removed {cnt} alices, because they didn't sign. Remaining: {round.InputCount}", round);

		await EndRoundAndTryCreateBlameRoundAsync(round, cancellationToken).ConfigureAwait(false);
	}

	private async Task FailFastTransactionSigningPhaseAsync(Round round, CancellationToken cancellationToken)
	{
		var alicesToRemove = round.Alices.Where(alice => !alice.ReadyToSign).ToHashSet();

		if (ReasonableOffendersCount(alicesToRemove.Count, round.Parameters.MinInputCountByRound))
		{
			foreach (var alice in alicesToRemove)
			{
				// Intentionally, do not ban Alices who have not signed, as clients using hardware wallets may not be able to sign in time.
				_prison.FailedToSignalReadyToSign(alice.Coin.Outpoint, alice.Coin.Amount, round.Id);
			}
		}
		else
		{
			Logger.LogWarning($"Tried to ban {alicesToRemove.Count} inputs for FailedToConfirm - ban was skipped.", round);
			foreach (var alice in alicesToRemove)
			{
				_prison.CoordinatorStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
			}
		}

		var removedAlices = round.Alices.RemoveAll(alice => alicesToRemove.Contains(alice));

		Logger.LogInfo($"Removed {removedAlices} alices, because they weren't ready. Remaining: {round.InputCount}", round);

		await EndRoundAndTryCreateBlameRoundAsync(round, cancellationToken).ConfigureAwait(false);
	}

	private async Task EndRoundAndTryCreateBlameRoundAsync(Round round, CancellationToken cancellationToken)
	{
		if (round.InputCount < _config.MinInputCountByBlameRound)
		{
			// There are not enough inputs, makes no sense to create the blame round.
			EndRound(round, EndRoundState.AbortedNotEnoughAlicesSigned);
			return;
		}

		// This indicates to the client that there will be a blame round.
		EndRound(round, EndRoundState.NotAllAlicesSign);

		FeeRate feeRate = await GetFeeRateEstimationAsync(cancellationToken).ConfigureAwait(false);
		var blameWhitelist = round.Alices
			.Select(x => x.Coin.Outpoint)
			.Where(x => !_prison.IsBanned(x, _config.GetDoSConfiguration(), DateTimeOffset.UtcNow))
			.ToHashSet();

		RoundParameters parameters = _roundParametersFactory(feeRate, round.Parameters.MaxSuggestedAmount) with
		{
			MinInputCountByRound = _config.MinInputCountByBlameRound
		};

		BlameRound blameRound = new(parameters, round, blameWhitelist, SecureRandom.Instance);
		AddRound(blameRound);
		Logger.LogInfo($"Blame round created from round '{round.Id}'.", blameRound);
	}

	private async Task CreateRoundsAsync(CancellationToken cancellationToken)
	{
		// Add more rounds if not enough.
		var registrableRoundCount = Rounds.Count(x => x is not BlameRound && x.Phase == Phase.InputRegistration && x.InputRegistrationTimeFrame.Remaining > TimeSpan.FromMinutes(1));
		int roundsToCreate = _config.RoundParallelization - registrableRoundCount;
		for (int i = 0; i < roundsToCreate; i++)
		{
			FeeRate feeRate = await GetFeeRateEstimationAsync(cancellationToken).ConfigureAwait(false);
			var parameters = _roundParametersFactory(feeRate, _maxSuggestedAmountProvider.MaxSuggestedAmount);

			var r = new Round(parameters, SecureRandom.Instance);
			AddRound(r);
			Logger.LogInfo($"Created round with parameters: {nameof(r.Parameters.MaxSuggestedAmount)}:'{r.Parameters.MaxSuggestedAmount}' BTC.", r);
		}
	}

	private void TimeoutRounds()
	{
		foreach (var expiredRound in Rounds.Where(
			x =>
			x.Phase == Phase.Ended
			&& x.End + _config.RoundExpiryTimeout < DateTimeOffset.UtcNow).ToArray())
		{
			Rounds.Remove(expiredRound);
		}
	}

	private void TimeoutAlices()
	{
		var now = DateTimeOffset.UtcNow;
		foreach (var round in Rounds.Where(x => !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound)).ToArray())
		{
			var alicesToRemove = round.Alices.Where(x => x.Deadline < now && !x.ConfirmedConnection).ToArray();
			foreach (var alice in alicesToRemove)
			{
				round.Alices.Remove(alice);
			}

			var removedAliceCount = alicesToRemove.Length;
			if (removedAliceCount > 0)
			{
				Logger.LogInfo($"{removedAliceCount} alices timed out and removed.", round);
			}
		}
	}

	public static ConstructionState AddCoordinationFee(Round round, ConstructionState coinjoin, Script coordinatorScriptPubKey)
	{
		var sizeToPayFor = coinjoin.EstimatedVsize + coordinatorScriptPubKey.EstimateOutputVsize();
		var miningFee = round.Parameters.MiningFeeRate.GetFee(sizeToPayFor) + Money.Satoshis(1);

		var availableCoordinationFee = coinjoin.Balance - miningFee;

		Logger.LogInfo($"Available coordination: {availableCoordinationFee}.", round);

		// The coordinator must pay output creation at round's FeeRate, but then he can wait to spend the output.
		var minEconomicalOutput = round.Parameters.MiningFeeRate.GetFee(coordinatorScriptPubKey.EstimateOutputVsize()) +
		                          new FeeRate(1.0m).GetFee(coordinatorScriptPubKey.EstimateInputVsize());

		if (availableCoordinationFee > minEconomicalOutput)
		{
			var txOut = new TxOut(availableCoordinationFee, coordinatorScriptPubKey);
			if (!txOut.IsDust())
			{
				return coinjoin.AddOutputNoMinAmountCheck(txOut)
					.AsPayingForSharedOverhead();
			}
		}

		Logger.LogWarning($"Available coordination fee wasn't taken, because it was too small: {availableCoordinationFee}.", round);
		return coinjoin;
	}

	private Script GetCoordinatorScriptPreventReuse(Round round)
	{
		var coordinatorScriptPubKey = _config.GetNextCleanCoordinatorScript();

		// Prevent coordinator script reuse.
		if (Rounds.Any(r => r.CoordinatorScript == coordinatorScriptPubKey))
		{
			_config.MakeNextCoordinatorScriptDirty();
			coordinatorScriptPubKey = _config.GetNextCleanCoordinatorScript();
			Logger.LogWarning("Coordinator script pub key was already used by another round, making it dirty and taking a new one.", round);
		}

		return coordinatorScriptPubKey;
	}

	private void AddRound(Round round)
	{
		Rounds.Add(round);
	}

	private void AbortDisruptedRounds()
	{
		while (_disruptedRounds.TryDequeue(out var disruptedRoundId))
		{
			var roundOrNull = Rounds.FirstOrDefault(x => x.Id == disruptedRoundId);
			if (roundOrNull is { } nonNullRound)
			{
				Logger.LogInfo("Round aborted because it was disrupted by double spenders.", nonNullRound);
				nonNullRound.EndRound(EndRoundState.AbortedDoubleSpendingDetected);
			}
		}
	}

	private void SetRoundPhase(Round round, Phase phase)
	{
		round.SetPhase(phase);
	}

	internal void EndRound(Round round, EndRoundState endRoundState)
	{
		round.EndRound(endRoundState);
	}

	private SigningState FinalizeTransaction(ConstructionState constructionState)
	{
		SigningState signingState = constructionState.Finalize();
		return signingState;
	}

	private async Task<FeeRate> GetFeeRateEstimationAsync(CancellationToken cancellationToken)
	{
		var feeEstimations = await _feeRateProvider(cancellationToken).ConfigureAwait(false);
		return feeEstimations.GetFeeRate((int)_config.ConfirmationTarget);
	}

	/// <summary>
	/// If too many inputs seem to misbehave, problem is probably on coordinator's side.
	/// Don't ban in that case to avoid huge amount of false-positives.
	/// </summary>
	private static bool ReasonableOffendersCount(int offendersCount, int minInputCount) => offendersCount <= minInputCount;
}
