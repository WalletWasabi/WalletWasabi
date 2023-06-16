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
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;
using WalletWasabi.WabiSabi.Backend.Events;
using WalletWasabi.Affiliation;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public partial class Arena : PeriodicRunner
{
	public Arena(
		TimeSpan period,
		WabiSabiConfig config,
		IRPCClient rpc,
		Prison prison,
		ICoinJoinIdStore coinJoinIdStore,
		RoundParameterFactory roundParameterFactory,
		CoinJoinTransactionArchiver? archiver = null,
		CoinJoinScriptStore? coinJoinScriptStore = null,
		CoinVerifier? coinVerifier = null) : base(period)
	{
		Config = config;
		Rpc = rpc;
		Prison = prison;
		TransactionArchiver = archiver;
		CoinJoinIdStore = coinJoinIdStore;
		CoinJoinScriptStore = coinJoinScriptStore;
		RoundParameterFactory = roundParameterFactory;
		CoinVerifier = coinVerifier;
		MaxSuggestedAmountProvider = new(Config);

		if (CoinVerifier is not null)
		{
			CoinVerifier.CoinBlacklisted += CoinVerifier_CoinBlacklisted;
		}
	}

	public event EventHandler<Transaction>? CoinJoinBroadcast;

	public event EventHandler<RoundCreatedEventArgs>? RoundCreated;

	public event EventHandler<CoinJoinTransactionCreatedEventArgs>? CoinJoinTransactionCreated;

	public event EventHandler<RoundPhaseChangedEventArgs>? RoundPhaseChanged;

	public event EventHandler<AffiliationAddedEventArgs>? AffiliationAdded;

	public event EventHandler<InputAddedEventArgs>? InputAdded;

	public HashSet<Round> Rounds { get; } = new();
	private IEnumerable<RoundState> RoundStates { get; set; } = Enumerable.Empty<RoundState>();
	private AsyncLock AsyncLock { get; } = new();
	private WabiSabiConfig Config { get; }
	internal IRPCClient Rpc { get; }
	private Prison Prison { get; }
	private CoinJoinTransactionArchiver? TransactionArchiver { get; }
	public CoinJoinScriptStore? CoinJoinScriptStore { get; }
	public CoinVerifier? CoinVerifier { get; private set; }
	private ICoinJoinIdStore CoinJoinIdStore { get; set; }
	private RoundParameterFactory RoundParameterFactory { get; }
	public MaxSuggestedAmountProvider MaxSuggestedAmountProvider { get; }

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var before = DateTimeOffset.UtcNow;
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
			SetRoundStates();
		}
		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("arena-period", duration);
	}

	private void SetRoundStates()
	{
		// Order rounds ascending by max suggested amount, then ascending by input count.
		// This will make sure WW2.0.1 clients register according to our desired order.
		var rounds = Rounds
						.OrderBy(x => x.Parameters.MaxSuggestedAmount)
						.ThenBy(x => x.InputCount)
						.ToList();

		RoundStates = rounds.Select(r => RoundState.FromRound(r, stateId: 0));
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
					if (offendingAlices.Any())
					{
						round.Alices.RemoveAll(x => offendingAlices.Contains(x));
					}
				}

				if (round is not BlameRound && CoinVerifier is not null)
				{
					try
					{
						var coinAliceDictionary = round.Alices.ToDictionary(alice => alice.Coin, alice => alice, CoinEqualityComparer.Default);
						foreach (var coinVerifyInfo in await CoinVerifier.VerifyCoinsAsync(coinAliceDictionary.Keys, cancel).ConfigureAwait(false))
						{
							if (coinVerifyInfo.ShouldRemove)
							{
								round.Alices.Remove(coinAliceDictionary[coinVerifyInfo.Coin]);
								CoinVerifier.VerifierAuditArchiver.LogRoundEvent(round.Id, $"{coinVerifyInfo.Coin.Outpoint} got removed from round");
							}
						}
					}
					catch (Exception exc)
					{
						// This should never happen.
						CoinVerifier.VerifierAuditArchiver.LogException(round.Id, exc);
						throw;
					}
				}

				if (round.InputCount < round.Parameters.MinInputCountByRound)
				{
					if (!round.InputRegistrationTimeFrame.HasExpired)
					{
						continue;
					}

					MaxSuggestedAmountProvider.StepMaxSuggested(round, false);
					EndRound(round, EndRoundState.AbortedNotEnoughAlices);
					round.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.InputRegistration)} phase. The minimum is ({round.Parameters.MinInputCountByRound}). {nameof(round.Parameters.MaxSuggestedAmount)} was '{round.Parameters.MaxSuggestedAmount}' BTC.");
				}
				else if (round.IsInputRegistrationEnded(round.Parameters.MaxInputCountByRound))
				{
					MaxSuggestedAmountProvider.StepMaxSuggested(round, true);
					SetRoundPhase(round, Phase.ConnectionConfirmation);
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
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
					SetRoundPhase(round, Phase.OutputRegistration);
				}
				else if (round.ConnectionConfirmationTimeFrame.HasExpired)
				{
					var alicesDidNotConfirm = round.Alices.Where(x => !x.ConfirmedConnection).ToArray();
					foreach (var alice in alicesDidNotConfirm)
					{
						Prison.Note(alice, round.Id);
					}
					var removedAliceCount = round.Alices.RemoveAll(x => alicesDidNotConfirm.Contains(x));
					round.LogInfo($"{removedAliceCount} alices removed because they didn't confirm.");

					// Once an input is confirmed and non-zero credentials are issued, it must be included and must provide a
					// a signature for a valid transaction to be produced, therefore this is the last possible opportunity to
					// remove any spent inputs.
					if (round.InputCount >= round.Parameters.MinInputCountByRound)
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

					if (round.InputCount < round.Parameters.MinInputCountByRound)
					{
						EndRound(round, EndRoundState.AbortedNotEnoughAlices);
						round.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.ConnectionConfirmation)} phase. The minimum is ({round.Parameters.MinInputCountByRound}).");
					}
					else
					{
						round.OutputRegistrationTimeFrame = TimeFrame.Create(Config.FailFastOutputRegistrationTimeout);
						SetRoundPhase(round, Phase.OutputRegistration);
					}
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
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

					var highestFeeRateTask = async () => (await Rpc.EstimateConservativeSmartFeeAsync(2, cancellationToken).ConfigureAwait(false)).FeeRate;

					coinjoin = await TryAddBlameScriptAsync(round, coinjoin, allReady, round.CoordinatorScript, highestFeeRateTask).ConfigureAwait(false);

					round.CoinjoinState = FinalizeTransaction(round.Id, coinjoin);

					if (!allReady && phaseExpired)
					{
						round.TransactionSigningTimeFrame = TimeFrame.Create(Config.FailFastTransactionSigningTimeout);
					}

					SetRoundPhase(round, Phase.TransactionSigning);
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
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
					round.LogInfo($"Network Fee: {networkFee.ToString(false, false)} BTC.");
					uint256 roundId = round.Id;
					FeeRate feeRate = coinjoin.GetFeeRate(spentCoins);
					round.LogInfo($"Network Fee Rate: {feeRate.SatoshiPerByte} sat/vByte.");
					round.LogInfo($"Desired Fee Rate: {round.Parameters.MiningFeeRate.SatoshiPerByte} sat/vByte.");

					// Added for monitoring reasons.
					try
					{
						FeeRate targetFeeRate = (await Rpc.EstimateConservativeSmartFeeAsync((int)Config.ConfirmationTarget, cancellationToken).ConfigureAwait(false)).FeeRate;
						round.LogInfo($"Current Fee Rate on the Network: {targetFeeRate.SatoshiPerByte} sat/vByte. Confirmation target is: {(int)Config.ConfirmationTarget} blocks.");
					}
					catch (Exception ex)
					{
						Logger.LogDebug($"Could not log fee rate monitoring: '{ex.Message}'.");
					}

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

					EndRound(round, EndRoundState.TransactionBroadcasted);
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
				EndRound(round, EndRoundState.TransactionBroadcastFailed);
			}
			catch (Exception ex)
			{
				round.LogWarning($"Signing phase failed, reason: '{ex}'.");
				EndRound(round, EndRoundState.AbortedWithError);
			}
		}
	}

	private async IAsyncEnumerable<Alice[]> CheckTxoSpendStatusAsync(Round round, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var chunkOfAlices in round.Alices.ToList().ChunkBy(16))
		{
			var batchedRpc = Rpc.PrepareBatch();

			var aliceCheckingTaskPairs = chunkOfAlices
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

		var alicesWhoDidNotSign = round.Alices
			.Where(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint))
			.ToHashSet();

		foreach (var alice in alicesWhoDidNotSign)
		{
			Prison.Note(alice, round.Id);
		}

		var cnt = round.Alices.RemoveAll(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint));

		round.LogInfo($"Removed {cnt} alices, because they didn't sign. Remaining: {round.InputCount}");

		if (round.InputCount >= round.Parameters.MinInputCountByRound)
		{
			EndRound(round, EndRoundState.NotAllAlicesSign);
			await CreateBlameRoundAsync(round, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			EndRound(round, EndRoundState.AbortedNotEnoughAlicesSigned);
		}
	}

	private async Task CreateBlameRoundAsync(Round round, CancellationToken cancellationToken)
	{
		var feeRate = (await Rpc.EstimateConservativeSmartFeeAsync((int)Config.ConfirmationTarget, cancellationToken).ConfigureAwait(false)).FeeRate;
		var blameWhitelist = round.Alices
			.Select(x => x.Coin.Outpoint)
			.Where(x => !Prison.IsBanned(x))
			.ToHashSet();

		RoundParameters parameters = RoundParameterFactory.CreateBlameRoundParameter(feeRate, round);
		BlameRound blameRound = new(parameters, round, blameWhitelist, SecureRandom.Instance);
		AddRound(blameRound);
		blameRound.LogInfo($"Blame round created from round '{round.Id}'.");
	}

	private async Task CreateRoundsAsync(CancellationToken cancellationToken)
	{
		FeeRate? feeRate = null;

		// Have rounds to split the volume around minimum input counts if load balance is required.
		// Only do things if the load balancer compatibility is configured.
		if (Config.WW200CompatibleLoadBalancing)
		{
			// Destroy the round when it reaches this input count and create 2 new ones instead.
			var roundDestroyerInputCount = Config.MinInputCountByRound * 2 + Config.MinInputCountByRound / 2;

			foreach (var round in Rounds.Where(x =>
				x.Phase == Phase.InputRegistration
				&& x is not BlameRound
				&& !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound)
				&& x.InputCount >= roundDestroyerInputCount).ToArray())
			{
				feeRate = (await Rpc.EstimateConservativeSmartFeeAsync((int)Config.ConfirmationTarget, cancellationToken).ConfigureAwait(false)).FeeRate;

				var allInputs = round.Alices.Select(y => y.Coin.Amount).OrderBy(x => x).ToArray();

				// 0.75 to bias towards larger numbers as larger input owners often have many smaller inputs too.
				var smallSuggestion = allInputs.Skip((int)(allInputs.Length * Config.WW200CompatibleLoadBalancingInputSplit)).First();
				var largeSuggestion = MaxSuggestedAmountProvider.AbsoluteMaximumInput;

				var roundWithoutThis = Rounds.Except(new[] { round });
				RoundParameters parameters = RoundParameterFactory.CreateRoundParameter(feeRate, largeSuggestion);
				Round? foundLargeRound = roundWithoutThis
					.FirstOrDefault(x =>
									x.Phase == Phase.InputRegistration
									&& x is not BlameRound
									&& !x.IsInputRegistrationEnded(round.Parameters.MaxInputCountByRound)
									&& x.Parameters.MaxSuggestedAmount >= allInputs.Max()
									&& x.InputRegistrationTimeFrame.Remaining > TimeSpan.FromSeconds(60));
				var largeRound = foundLargeRound ?? TryMineRound(parameters, roundWithoutThis.ToArray());

				if (largeRound is not null)
				{
					parameters = RoundParameterFactory.CreateRoundParameter(feeRate, smallSuggestion);
					var smallRound = TryMineRound(parameters, roundWithoutThis.Concat(new[] { largeRound }).ToArray());

					// If creation is successful, only then destroy the round.
					if (smallRound is not null)
					{
						AddRound(largeRound);
						AddRound(smallRound);

						if (foundLargeRound is null)
						{
							largeRound.LogInfo($"Mined round with parameters: {nameof(largeRound.Parameters.MaxSuggestedAmount)}:'{largeRound.Parameters.MaxSuggestedAmount}' BTC.");
						}
						smallRound.LogInfo($"Mined round with parameters: {nameof(smallRound.Parameters.MaxSuggestedAmount)}:'{smallRound.Parameters.MaxSuggestedAmount}' BTC.");

						// If it can't create the large round, then don't abort.
						EndRound(round, EndRoundState.AbortedLoadBalancing);
						Logger.LogInfo($"Destroyed round with {allInputs.Length} inputs. Threshold: {roundDestroyerInputCount}");
					}
				}
			}
		}

		// Add more rounds if not enough.
		var registrableRoundCount = Rounds.Count(x => x is not BlameRound && x.Phase == Phase.InputRegistration && x.InputRegistrationTimeFrame.Remaining > TimeSpan.FromMinutes(1));
		int roundsToCreate = Config.RoundParallelization - registrableRoundCount;
		for (int i = 0; i < roundsToCreate; i++)
		{
			feeRate ??= (await Rpc.EstimateConservativeSmartFeeAsync((int)Config.ConfirmationTarget, cancellationToken).ConfigureAwait(false)).FeeRate;
			RoundParameters parameters = RoundParameterFactory.CreateRoundParameter(feeRate, MaxSuggestedAmountProvider.MaxSuggestedAmount);

			var r = new Round(parameters, SecureRandom.Instance);
			AddRound(r);
			r.LogInfo($"Created round with parameters: {nameof(r.Parameters.MaxSuggestedAmount)}:'{r.Parameters.MaxSuggestedAmount}' BTC.");
		}
	}

	private Round? TryMineRound(RoundParameters parameters, Round[] rounds)
	{
		// Huge HACK to keep it compatible with WW2.0.0 client version, which's
		// round preference is based on the ordering of ToImmutableDictionary.
		// Add round until ToImmutableDictionary orders it to be the first round
		// so old clients will prefer that one.
		IOrderedEnumerable<Round>? orderedRounds;
		Round r;
		var before = DateTimeOffset.UtcNow;
		var times = 0;
		var maxCycleTimes = 300;
		do
		{
			var roundsCopy = rounds.ToList();
			r = new Round(parameters, SecureRandom.Instance);
			roundsCopy.Add(r);
			orderedRounds = roundsCopy
				.Where(x => x.Phase == Phase.InputRegistration && x is not BlameRound && !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound))
				.OrderBy(x => x.Parameters.MaxSuggestedAmount)
				.ThenBy(x => x.InputCount);
			times++;
		}
		while (times <= maxCycleTimes && orderedRounds.ToImmutableDictionary(x => x.Id, x => x).First().Key != r.Id);

		Logger.LogDebug($"First ordered round creator did {times} cycles.");

		if (times > maxCycleTimes)
		{
			r.LogInfo("First ordered round creation too expensive. Skipping...");
			return null;
		}
		else
		{
			return r;
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
		foreach (var round in Rounds.Where(x => !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound)).ToArray())
		{
			var alicesToRemove = round.Alices.Where(x => x.Deadline < DateTimeOffset.UtcNow).ToArray();
			foreach (var alice in alicesToRemove)
			{
				round.Alices.Remove(alice);
				CoinVerifier?.CancelSchedule(alice.Coin);
			}

			var removedAliceCount = alicesToRemove.Length;
			if (removedAliceCount > 0)
			{
				round.LogInfo($"{removedAliceCount} alices timed out and removed.");
			}
		}
	}

	internal static async Task<ConstructionState> TryAddBlameScriptAsync(Round round, ConstructionState coinjoin, bool allReady, Script blameScript, Func<Task<FeeRate>> highestFeeRateAsyncMethod)
	{
		// SharedOverhead calculated into EstimatedVsize.
		var sizeToPayFor = coinjoin.EstimatedVsize + blameScript.EstimateOutputVsize();
		var miningFee = sizeToPayFor == 0
			? Money.Zero
			: round.Parameters.MiningFeeRate.GetFee(sizeToPayFor);

		// Subtract 1 sat to avoid off-by-one error coming from roundings.
		var diffMoney = coinjoin.Balance - miningFee - Money.Satoshis(1);

		if (diffMoney > round.Parameters.AllowedOutputAmounts.Min)
		{
			// If diff is smaller than max fee rate of a tx, then add it as fee.
			var highestFeeRate = await highestFeeRateAsyncMethod().ConfigureAwait(false);

			// ToDo: This condition could be more sophisticated by always trying to max out the miner fees to target 2 and only deal with the remaining diffMoney.
			if (coinjoin.EffectiveFeeRate > highestFeeRate)
			{
				coinjoin = coinjoin.AddOutput(new TxOut(diffMoney, blameScript)).AsPayingForSharedOverhead();

				if (allReady)
				{
					round.LogInfo($"Filled up the outputs to build a reasonable transaction, all Alices signaled ready. Added amount: '{diffMoney}'.");
				}
				else
				{
					round.LogWarning($"Filled up the outputs to build a reasonable transaction because some alice failed to provide its output. Added amount: '{diffMoney}'.");
				}
			}
			else
			{
				if (allReady)
				{
					round.LogInfo($"There were some leftover satoshis. Added amount to miner fees: '{diffMoney}'.");
				}
				else
				{
					round.LogWarning($"Some alices failed to signal ready. There were some leftover satoshis. Added amount to miner fees: '{diffMoney}'.");
				}
			}
		}
		else if (!allReady)
		{
			round.LogWarning($"Could not add blame script, because the amount was too small: {diffMoney}.");
		}

		return coinjoin;
	}

	private ConstructionState AddCoordinationFee(Round round, ConstructionState coinjoin, Script coordinatorScriptPubKey)
	{
		var coordinationFee = round.Alices.Where(a => !a.IsCoordinationFeeExempted).Sum(x => round.Parameters.CoordinationFeeRate.GetFee(x.Coin.Amount));
		if (coordinationFee == 0)
		{
			round.LogInfo($"Coordination fee wasn't taken, because it was free for everyone. Hurray!");
		}
		else
		{
			var effectiveCoordinationFee = coordinationFee - round.Parameters.MiningFeeRate.GetFee(coordinatorScriptPubKey.EstimateOutputVsize() + coinjoin.UnpaidSharedOverhead);

			if (effectiveCoordinationFee > round.Parameters.AllowedOutputAmounts.Min)
			{
				coinjoin = coinjoin.AddOutput(new TxOut(effectiveCoordinationFee, coordinatorScriptPubKey)).AsPayingForSharedOverhead();
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

	private void CoinVerifier_CoinBlacklisted(object? _, Coin coin)
	{
		// For logging reason Prison needs the roundId.
		var roundState = RoundStates.FirstOrDefault(rs => rs.CoinjoinState.Inputs.Any(input => input.Outpoint == coin.Outpoint));

		// Could be a coin from WW1.
		var roundId = roundState?.Id ?? uint256.Zero;
		Prison.Ban(coin.Outpoint, roundId, isLongBan: true);
	}

	private void AddRound(Round round)
	{
		Rounds.Add(round);
		RoundCreated?.SafeInvoke(this, new RoundCreatedEventArgs(round.Id, round.Parameters));
	}

	private void SetRoundPhase(Round round, Phase phase)
	{
		round.SetPhase(phase);

		if (phase == Phase.OutputRegistration)
		{
			foreach (Alice alice in round.Alices)
			{
				NotifyInput(round.Id, alice.Coin, alice.IsCoordinationFeeExempted);
			}
		}

		RoundPhaseChanged?.SafeInvoke(this, new RoundPhaseChangedEventArgs(round.Id, phase));
	}

	private void EndRound(Round round, EndRoundState endRoundState)
	{
		round.EndRound(endRoundState);
		RoundPhaseChanged?.SafeInvoke(this, new RoundPhaseChangedEventArgs(round.Id, Phase.Ended));
	}

	private void NotifyInput(uint256 roundId, Coin coin, bool isCoordinationFeeExempted)
	{
		InputAdded.SafeInvoke(this, new InputAddedEventArgs(roundId, coin, isCoordinationFeeExempted));
	}

	private void NotifyAffiliation(uint256 roundId, Coin coin, string affiliationId)
	{
		AffiliationAdded.SafeInvoke(this, new AffiliationAddedEventArgs(roundId, coin, affiliationId));
	}

	private SigningState FinalizeTransaction(uint256 roundId, ConstructionState constructionState)
	{
		SigningState signingState = constructionState.Finalize();
		CoinJoinTransactionCreated?.SafeInvoke(this, new CoinJoinTransactionCreatedEventArgs(roundId, signingState.CreateTransaction()));
		return signingState;
	}

	public override void Dispose()
	{
		if (CoinVerifier is not null)
		{
			CoinVerifier.CoinBlacklisted -= CoinVerifier_CoinBlacklisted;
		}
		base.Dispose();
	}
}
