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
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WabiSabi.Backend.Rounds.Utils;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public partial class Arena : PeriodicRunner
	{
		public Arena(
			TimeSpan period,
			Network network,
			WabiSabiConfig config,
			IRPCClient rpc,
			Prison prison,
			CoinJoinTransactionArchiver? archiver = null) : base(period)
		{
			Network = network;
			Config = config;
			Rpc = rpc;
			Prison = prison;
			TransactionArchiver = archiver;
			Random = new SecureRandom();
		}

		public HashSet<Round> Rounds { get; } = new();
		private AsyncLock AsyncLock { get; } = new();
		private Network Network { get; }
		private WabiSabiConfig Config { get; }
		private IRPCClient Rpc { get; }
		private Prison Prison { get; }
		private SecureRandom Random { get; }
		private CoinJoinTransactionArchiver? TransactionArchiver { get; }

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
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
						round.SetPhase(Phase.Ended);
						round.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.InputRegistration)} phase.");
					}
					else if (round.IsInputRegistrationEnded(Config.MaxInputCountByRound))
					{
						round.SetPhase(Phase.ConnectionConfirmation);
					}
				}
				catch (Exception ex)
				{
					round.SetPhase(Phase.Ended);
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
							round.SetPhase(Phase.Ended);
							round.LogInfo($"Not enough inputs ({round.InputCount}) in {nameof(Phase.ConnectionConfirmation)} phase.");
						}
						else
						{
							round.SetPhase(Phase.OutputRegistration);
						}
					}
				}
				catch (Exception ex)
				{
					round.SetPhase(Phase.Ended);
					round.LogError(ex.Message);
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

					if (allReady || round.OutputRegistrationTimeFrame.HasExpired)
					{
						var coinjoin = round.Assert<ConstructionState>();

						round.LogInfo($"{coinjoin.Inputs.Count} inputs were added.");
						round.LogInfo($"{coinjoin.Outputs.Count} outputs were added.");

						long aliceSum = round.Alices.Sum(x => x.CalculateRemainingAmountCredentials(round.FeeRate));
						long bobSum = round.Bobs.Sum(x => x.CredentialAmount);
						var diff = aliceSum - bobSum;

						// If timeout we must fill up the outputs to build a reasonable transaction.
						// This won't be signed by the alice who failed to provide output, so we know who to ban.
						var diffMoney = Money.Satoshis(diff) - coinjoin.Parameters.FeeRate.GetFee(Config.BlameScript.EstimateOutputVsize());
						if (!allReady && diffMoney > coinjoin.Parameters.AllowedOutputAmounts.Min)
						{
							coinjoin = coinjoin.AddOutput(new TxOut(diffMoney, Config.BlameScript));
							round.LogInfo("Filled up the outputs to build a reasonable transaction because some alice failed to provide its output.");
						}

						round.CoinjoinState = coinjoin.Finalize();

						round.SetPhase(Phase.TransactionSigning);
					}
				}
				catch (Exception ex)
				{
					round.SetPhase(Phase.Ended);
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
						round.LogInfo($"Network Fee Rate: {feeRate.FeePerK.ToDecimal(MoneyUnit.Satoshi) / 1000} sat/vByte.");
						round.LogInfo($"Number of inputs: {coinjoin.Inputs.Count}.");
						round.LogInfo($"Number of outputs: {coinjoin.Outputs.Count}.");
						round.LogInfo($"Serialized Size: {coinjoin.GetSerializedSize() / 1024} KB.");
						round.LogInfo($"VSize: {coinjoin.GetVirtualSize() / 1024} KB.");
						var indistinguishableOutputs = coinjoin.GetIndistinguishableOutputs(includeSingle: true);
						foreach (var (value, count) in indistinguishableOutputs.Where(x => x.count > 1))
						{
							round.LogInfo($"There are {count} occurrences of {value.ToString(true, false)} outputs.");
						}
						round.LogInfo($"There are {indistinguishableOutputs.Count(x => x.count == 1)} occurrences of unique outputs.");

						// Store transaction.
						if (TransactionArchiver is not null)
						{
							await TransactionArchiver.StoreJsonAsync(coinjoin).ConfigureAwait(false);
						}

						// Broadcasting.
						await Rpc.SendRawTransactionAsync(coinjoin, cancellationToken).ConfigureAwait(false);
						round.WasTransactionBroadcast = true;
						round.SetPhase(Phase.Ended);

						round.LogInfo($"Successfully broadcast the CoinJoin: {coinjoin.GetHash()}.");
					}
					else if (round.TransactionSigningTimeFrame.HasExpired)
					{
						throw new TimeoutException($"Round {round.Id}: Signing phase timed out after {round.TransactionSigningTimeFrame.Duration.TotalSeconds} seconds.");
					}
				}
				catch (Exception ex)
				{
					round.LogWarning($"Signing phase failed, reason: '{ex}'.");
					await FailTransactionSigningPhaseAsync(round, cancellationToken).ConfigureAwait(false);
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

			var unsignedPrevouts = state.UnsignedInputs.ToHashSet();

			var alicesWhoDidntSign = round.Alices
				.Select(alice => (Alice: alice, alice.Coin))
				.Where(x => unsignedPrevouts.Contains(x.Coin))
				.Select(x => x.Alice)
				.ToHashSet();

			foreach (var alice in alicesWhoDidntSign)
			{
				Prison.Note(alice, round.Id);
			}

			round.Alices.RemoveAll(x => alicesWhoDidntSign.Contains(x));
			round.SetPhase(Phase.Ended);

			if (round.InputCount >= Config.MinInputCountByRound)
			{
				await CreateBlameRoundAsync(round, cancellationToken).ConfigureAwait(false);
			}
		}

		private async Task CreateBlameRoundAsync(Round round, CancellationToken cancellationToken)
		{
			var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancellationToken).ConfigureAwait(false)).FeeRate;
			RoundParameters parameters = new(Config, Network, Random, feeRate);
			var blameWhitelist = round.Alices
				.Select(x => x.Coin.Outpoint)
				.Where(x => !Prison.IsBanned(x))
				.ToHashSet();

			BlameRound blameRound = new(parameters, round, blameWhitelist);
			Rounds.Add(blameRound);
		}

		private async Task CreateRoundsAsync(CancellationToken cancellationToken)
		{
			if (!Rounds.Any(x => x is not BlameRound && x.Phase == Phase.InputRegistration))
			{
				var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancellationToken).ConfigureAwait(false)).FeeRate;

				RoundParameters roundParams = new(Config, Network, Random, feeRate);
				Round r = new(roundParams);
				Rounds.Add(r);
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

		public override void Dispose()
		{
			Random.Dispose();
			base.Dispose();
		}
	}
}
