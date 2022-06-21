using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;
using System.Collections.Immutable;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public partial class Arena : PeriodicRunner
{
	public Arena(
		TimeSpan period,
		Network network,
		WabiSabiConfig config,
		IRPCClient rpc,
		Prison prison,
		ICoinJoinIdStore coinJoinIdStore,
		RoundParameterFactory roundParameterFactory,
		CoinJoinTransactionArchiver? archiver = null,
		CoinJoinScriptStore? coinJoinScriptStore = null) : base(period)
	{
		Network = network;
		Config = config;
		Rpc = rpc;
		Prison = prison;
		TransactionArchiver = archiver;
		CoinJoinIdStore = coinJoinIdStore;
		CoinJoinScriptStore = coinJoinScriptStore;
		RoundParameterFactory = roundParameterFactory;
		MaxSuggestedAmountProvider = new(Config);
	}

	public event EventHandler<Transaction>? CoinJoinBroadcast;

	public HashSet<Round> Rounds { get; } = new();
	private ImmutableArray<RoundState> RoundStates { get; set; } = ImmutableArray.Create<RoundState>();
	private AsyncLock AsyncLock { get; } = new();
	private Network Network { get; }
	private WabiSabiConfig Config { get; }
	internal IRPCClient Rpc { get; }
	private Prison Prison { get; }
	private CoinJoinTransactionArchiver? TransactionArchiver { get; }
	public CoinJoinScriptStore? CoinJoinScriptStore { get; }
	private ICoinJoinIdStore CoinJoinIdStore { get; set; }
	private RoundParameterFactory RoundParameterFactory { get; }
	public MaxSuggestedAmountProvider MaxSuggestedAmountProvider { get; }

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
		{
			TimeoutRounds();

			TimeoutAlices();

			await StepTransactionSigningPhaseAsync(cancel).ConfigureAwait(false);

			await StepOutputRegistrationPhaseAsync(cancel).ConfigureAwait(false);

			await StepConnectionConfirmationPhaseAsync(cancel).ConfigureAwait(false);

			await StepInputRegistrationPhaseAsync(cancel).ConfigureAwait(false);

			cancel.ThrowIfCancellationRequested();

			// Ensure there's at least one non-blame round in input registration.
			await CreateRoundsAsync(cancel).ConfigureAwait(false);

			// RoundStates have to contain all states. Do not change stateId=0.
			RoundStates = Rounds.Select(r => RoundState.FromRound(r, stateId: 0)).ToImmutableArray();
		}
	}

	private async Task StepInputRegistrationPhaseAsync(CancellationToken cancel)
	{
		foreach (var round in Rounds.Where(x =>
			x.Phase == Phase.InputRegistration
			&& x.IsInputRegistrationEnded(Config.MaxInputCountByRound))
			.ToArray())
		{
			try
			{
				await foreach (var offendingAlices in CheckTxoSpendStatusAsync(round, cancel).ConfigureAwait(false))
				{
					if (offendingAlices.Any())
					{
						round.Alices.RemoveAll(x => offendingAlices.Contains(x));
					}
				}

				if (round.InputCount < Config.MinInputCountByRound)
				{
					if (!round.InputRegistrationTimeFrame.HasExpired)
					{
						continue;
					}

					MaxSuggestedAmountProvider.StepMaxSuggested(round, false);
					round.EndRound(EndRoundState.AbortedNotEnoughAlices);
					round.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.InputRegistration)} phase. The minimum is ({Config.MinInputCountByRound}). {nameof(round.Parameters.MaxSuggestedAmount)} was '{round.Parameters.MaxSuggestedAmount}' BTC.");
				}
				else if (round.IsInputRegistrationEnded(Config.MaxInputCountByRound))
				{
					MaxSuggestedAmountProvider.StepMaxSuggested(round, true);
					round.SetPhase(Phase.ConnectionConfirmation);
				}
			}
			catch (Exception ex)
			{
				round.EndRound(EndRoundState.AbortedWithError);
				round.LogError(ex.Message);
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
					round.SetPhase(Phase.OutputRegistration);
				}
				else if (round.ConnectionConfirmationTimeFrame.HasExpired)
				{
					var alicesDidntConfirm = round.Alices.Where(x => !x.ConfirmedConnection).ToArray();
					foreach (var alice in alicesDidntConfirm)
					{
						Prison.Note(alice, round.Id);
					}
					var removedAliceCount = round.Alices.RemoveAll(x => alicesDidntConfirm.Contains(x));
					round.LogInfo($"{removedAliceCount} alices removed because they didn't confirm.");

					// Once an input is confirmed and non-zero credentials are issued, it must be included and must provide a
					// a signature for a valid transaction to be produced, therefore this is the last possible opportunity to
					// remove any spent inputs.
					if (round.InputCount >= Config.MinInputCountByRound)
					{
						await foreach (var offendingAlices in CheckTxoSpendStatusAsync(round, cancel).ConfigureAwait(false))
						{
							if (offendingAlices.Any())
							{
								var removed = round.Alices.RemoveAll(x => offendingAlices.Contains(x));
								round.LogInfo($"There were {removed} alices removed because they spent the registered UTXO.");
							}
						}
					}

					if (round.InputCount < Config.MinInputCountByRound)
					{
						round.EndRound(EndRoundState.AbortedNotEnoughAlices);
						round.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.ConnectionConfirmation)} phase. The minimum is ({Config.MinInputCountByRound}).");
					}
					else
					{
						round.OutputRegistrationTimeFrame = TimeFrame.Create(Config.FailFastOutputRegistrationTimeout);

						// Cliens misbehave when they don't confirm everything.
						if (round is BlameRound)
						{
							// Unfortunately we would stop the blame round chain completely so we must go to output registration even though we know we'll fail.
							round.SetPhase(Phase.OutputRegistration);
						}
						else
						{
							round.EndRound(EndRoundState.AbortedNotAllAlicesConfirmed);
						}
					}
				}
			}
			catch (Exception ex)
			{
				round.EndRound(EndRoundState.AbortedWithError);
				round.LogError(ex.Message);
			}
		}
	}

	private async Task StepOutputRegistrationPhaseAsync(CancellationToken cancellationToken)
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

					round.LogInfo($"{coinjoin.Inputs.Count()} inputs were added.");
					round.LogInfo($"{coinjoin.Outputs.Count()} outputs were added.");

					round.CoordinatorScript = GetCoordinatorScriptPreventReuse(round);
					coinjoin = AddCoordinationFee(round, coinjoin, round.CoordinatorScript);

					coinjoin = await TryAddBlameScriptAsync(round, coinjoin, allReady, round.CoordinatorScript, cancellationToken).ConfigureAwait(false);

					round.CoinjoinState = coinjoin.Finalize();

					if (!allReady && phaseExpired)
					{
						round.TransactionSigningTimeFrame = TimeFrame.Create(Config.FailFastTransactionSigningTimeout);
					}

					round.SetPhase(Phase.TransactionSigning);
				}
			}
			catch (Exception ex)
			{
				round.EndRound(EndRoundState.AbortedWithError);
				round.LogError(ex.Message);
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
					round.LogInfo("Trying to broadcast coinjoin.");
					Coin[]? spentCoins = round.Alices.Select(x => x.Coin).ToArray();
					Money networkFee = coinjoin.GetFee(spentCoins);
					uint256 roundId = round.Id;
					FeeRate feeRate = coinjoin.GetFeeRate(spentCoins);
					round.LogInfo($"Network Fee: {networkFee.ToString(false, false)} BTC.");
					round.LogInfo(
						$"Network Fee Rate: {feeRate.FeePerK.ToDecimal(MoneyUnit.Satoshi) / 1000} sat/vByte.");
					round.LogInfo($"Number of inputs: {coinjoin.Inputs.Count}.");
					round.LogInfo($"Number of outputs: {coinjoin.Outputs.Count}.");
					round.LogInfo($"Serialized Size: {coinjoin.GetSerializedSize() / 1024} KB.");
					round.LogInfo($"VSize: {coinjoin.GetVirtualSize() / 1024} KB.");
					var indistinguishableOutputs = coinjoin.GetIndistinguishableOutputs(includeSingle: true);
					foreach (var (value, count) in indistinguishableOutputs.Where(x => x.count > 1))
					{
						round.LogInfo($"There are {count} occurrences of {value.ToString(true, false)} outputs.");
					}

					round.LogInfo(
						$"There are {indistinguishableOutputs.Count(x => x.count == 1)} occurrences of unique outputs.");

					// Store transaction.
					if (TransactionArchiver is not null)
					{
						await TransactionArchiver.StoreJsonAsync(coinjoin).ConfigureAwait(false);
					}

					// Broadcasting.
					await Rpc.SendRawTransactionAsync(coinjoin, cancellationToken).ConfigureAwait(false);

					var coordinatorScriptPubKey = Config.GetNextCleanCoordinatorScript();
					if (round.CoordinatorScript == coordinatorScriptPubKey)
					{
						Config.MakeNextCoordinatorScriptDirty();
					}

					foreach (var address in coinjoin.Outputs
								 .Select(x => x.ScriptPubKey)
								 .Where(script => CoinJoinScriptStore?.Contains(script) is true))
					{
						if (address == round.CoordinatorScript)
						{
							round.LogError(
								$"Coordinator script pub key reuse detected: {round.CoordinatorScript.ToHex()}");
						}
						else
						{
							round.LogError($"Output script pub key reuse detected: {address.ToHex()}");
						}
					}

					round.EndRound(EndRoundState.TransactionBroadcasted);
					round.LogInfo($"Successfully broadcast the coinjoin: {coinjoin.GetHash()}.");

					CoinJoinScriptStore?.AddRange(coinjoin.Outputs.Select(x => x.ScriptPubKey));
					CoinJoinBroadcast?.Invoke(this, coinjoin);
				}
				else if (round.TransactionSigningTimeFrame.HasExpired)
				{
					round.LogWarning($"Signing phase failed with timed out after {round.TransactionSigningTimeFrame.Duration.TotalSeconds} seconds.");
					await FailTransactionSigningPhaseAsync(round, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (RPCException ex)
			{
				round.LogWarning($"Transaction broadcasting failed: '{ex}'.");
				round.EndRound(EndRoundState.TransactionBroadcastFailed);
			}
			catch (Exception ex)
			{
				round.LogWarning($"Signing phase failed, reason: '{ex}'.");
				round.EndRound(EndRoundState.AbortedWithError);
			}
		}
	}

	private async IAsyncEnumerable<Alice[]> CheckTxoSpendStatusAsync(Round round, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var chunckOfAlices in round.Alices.ToList().ChunkBy(16))
		{
			var batchedRpc = Rpc.PrepareBatch();

			var aliceCheckingTaskPairs = chunckOfAlices
				.Select(x => (Alice: x, StatusTask: Rpc.GetTxOutAsync(x.Coin.Outpoint.Hash, (int)x.Coin.Outpoint.N, includeMempool: true, cancellationToken)))
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

		var alicesWhoDidntSign = round.Alices
			.Where(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint))
			.ToHashSet();

		foreach (var alice in alicesWhoDidntSign)
		{
			Prison.Note(alice, round.Id);
		}

		var cnt = round.Alices.RemoveAll(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint));

		round.LogInfo($"Removed {cnt} alices, because they didn't sign. Remainig: {round.InputCount}");

		if (round.InputCount >= Config.MinInputCountByRound)
		{
			round.EndRound(EndRoundState.NotAllAlicesSign);
			await CreateBlameRoundAsync(round, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			round.EndRound(EndRoundState.AbortedNotEnoughAlicesSigned);
		}
	}

	private async Task CreateBlameRoundAsync(Round round, CancellationToken cancellationToken)
	{
		var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancellationToken).ConfigureAwait(false)).FeeRate;
		var blameWhitelist = round.Alices
			.Select(x => x.Coin.Outpoint)
			.Where(x => !Prison.IsBanned(x))
			.ToHashSet();

		RoundParameters parameters = RoundParameterFactory.CreateBlameRoundParameter(feeRate, round);
		BlameRound blameRound = new(parameters, round, blameWhitelist, SecureRandom.Instance);
		Rounds.Add(blameRound);
		blameRound.LogInfo("Blame round created.");
	}

	private async Task CreateRoundsAsync(CancellationToken cancellationToken)
	{
		if (!Rounds.Any(x => x is not BlameRound && x.Phase == Phase.InputRegistration))
		{
			var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancellationToken).ConfigureAwait(false)).FeeRate;

			RoundParameters parameters =
				RoundParameterFactory.CreateRoundParameter(feeRate, MaxSuggestedAmountProvider.MaxSuggestedAmount);
			Round r = new(parameters, SecureRandom.Instance);
			Rounds.Add(r);
			r.LogInfo($"Created round with params: {nameof(parameters.MaxSuggestedAmount)}:'{parameters.MaxSuggestedAmount}' BTC.");
		}
	}

	private void TimeoutRounds()
	{
		foreach (var expiredRound in Rounds.Where(
			x =>
			x.Phase == Phase.Ended
			&& x.End + Config.RoundExpiryTimeout < DateTimeOffset.UtcNow).ToArray())
		{
			Rounds.Remove(expiredRound);
		}
	}

	private void TimeoutAlices()
	{
		foreach (var round in Rounds.Where(x => !x.IsInputRegistrationEnded(Config.MaxInputCountByRound)).ToArray())
		{
			var removedAliceCount = round.Alices.RemoveAll(x => x.Deadline < DateTimeOffset.UtcNow);
			if (removedAliceCount > 0)
			{
				round.LogInfo($"{removedAliceCount} alices timed out and removed.");
			}
		}
	}

	private async Task<ConstructionState> TryAddBlameScriptAsync(Round round, ConstructionState coinjoin, bool allReady, Script blameScript, CancellationToken cancellationToken)
	{
		long aliceSum = round.Alices.Sum(x => x.CalculateRemainingAmountCredentials(round.Parameters.MiningFeeRate, round.Parameters.CoordinationFeeRate));
		long bobSum = round.Bobs.Sum(x => x.CredentialAmount);
		var diff = aliceSum - bobSum;

		// If timeout we must fill up the outputs to build a reasonable transaction.
		// This won't be signed by the alice who failed to provide output, so we know who to ban.
		var diffMoney = Money.Satoshis(diff) - round.Parameters.MiningFeeRate.GetFee(blameScript.EstimateOutputVsize());
		if (diffMoney > round.Parameters.AllowedOutputAmounts.Min)
		{
			// If diff is smaller than max fee rate of a tx, then add it as fee.
			var highestFeeRate = (await Rpc.EstimateSmartFeeAsync(2, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancellationToken).ConfigureAwait(false)).FeeRate;

			// ToDo: This condition could be more sophisticated by always trying to max out the miner fees to target 2 and only deal with the remaining diffMoney.
			if (coinjoin.EffectiveFeeRate > highestFeeRate)
			{
				coinjoin = coinjoin.AddOutput(new TxOut(diffMoney, blameScript));

				if (allReady)
				{
					round.LogInfo($"Filled up the outputs to build a reasonable transaction, all Alices signalled ready. Added amount: '{diffMoney}'.");
				}
				else
				{
					round.LogWarning($"Filled up the outputs to build a reasonable transaction because some alice failed to provide its output. Added amount: '{diffMoney}'.");
				}
			}
			else
			{
				round.LogWarning($"There were some leftover satoshis. Added amount to miner fees: '{diffMoney}'.");
			}
		}
		else if (!allReady)
		{
			round.LogWarning($"Could not add blame script, because the amount was too small: {nameof(diffMoney)}: {diffMoney}.");
		}

		return coinjoin;
	}

	private ConstructionState AddCoordinationFee(Round round, ConstructionState coinjoin, Script coordinatorScriptPubKey)
	{
		var coordinationFee = round.Alices.Where(a => !a.IsPayingZeroCoordinationFee).Sum(x => round.Parameters.CoordinationFeeRate.GetFee(x.Coin.Amount));
		if (coordinationFee == 0)
		{
			round.LogInfo($"Coordination fee wasn't taken, because it was free for everyone. Hurray!");
		}
		else
		{
			var effectiveCoordinationFee = coordinationFee - round.Parameters.MiningFeeRate.GetFee(coordinatorScriptPubKey.EstimateOutputVsize());

			if (effectiveCoordinationFee > round.Parameters.AllowedOutputAmounts.Min)
			{
				coinjoin = coinjoin.AddOutput(new TxOut(effectiveCoordinationFee, coordinatorScriptPubKey));
			}
			else
			{
				round.LogWarning($"Effective coordination fee wasn't taken, because it was too small: {effectiveCoordinationFee}.");
			}
		}

		return coinjoin;
	}

	private Script GetCoordinatorScriptPreventReuse(Round round)
	{
		var coordinatorScriptPubKey = Config.GetNextCleanCoordinatorScript();

		// Prevent coordinator script reuse.
		if (Rounds.Any(r => r.CoordinatorScript == coordinatorScriptPubKey))
		{
			Config.MakeNextCoordinatorScriptDirty();
			coordinatorScriptPubKey = Config.GetNextCleanCoordinatorScript();
			round.LogWarning("Coordinator script pub key was already used by another round, making it dirty and taking a new one.");
		}

		return coordinatorScriptPubKey;
	}
}
