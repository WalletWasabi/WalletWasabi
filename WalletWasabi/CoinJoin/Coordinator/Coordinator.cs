using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.CoinJoin.Coordinator.Banning;
using WalletWasabi.CoinJoin.Coordinator.Participants;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.CoinJoin.Coordinator
{
	public class Coordinator : IDisposable
	{
		private List<CoordinatorRound> Rounds { get; }
		private AsyncLock RoundsListLock { get; }

		private List<uint256> CoinJoins { get; }
		public string CoinJoinsFilePath => Path.Combine(FolderPath, $"CoinJoins{Network}.txt");
		private AsyncLock CoinJoinsLock { get; }

		public event EventHandler<Transaction> CoinJoinBroadcasted;

		public RPCClient RpcClient { get; }

		public CoordinatorRoundConfig RoundConfig { get; private set; }

		public Network Network { get; }

		public TrustedNodeNotifyingBehavior TrustedNodeNotifyingBehavior { get; }

		public string FolderPath { get; }

		public UtxoReferee UtxoReferee { get; }

		public Coordinator(Network network, TrustedNodeNotifyingBehavior trustedNodeNotifyingBehavior, string folderPath, RPCClient rpc, CoordinatorRoundConfig roundConfig)
		{
			Network = Guard.NotNull(nameof(network), network);
			TrustedNodeNotifyingBehavior = Guard.NotNull(nameof(trustedNodeNotifyingBehavior), trustedNodeNotifyingBehavior);
			FolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(folderPath), folderPath, trim: true);
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);

			Rounds = new List<CoordinatorRound>();
			RoundsListLock = new AsyncLock();

			CoinJoins = new List<uint256>();
			CoinJoinsLock = new AsyncLock();

			Directory.CreateDirectory(FolderPath);

			UtxoReferee = new UtxoReferee(Network, FolderPath, RpcClient, RoundConfig);

			if (File.Exists(CoinJoinsFilePath))
			{
				try
				{
					var getTxTasks = new List<(Task<Transaction> txTask, string line)>();
					var batch = RpcClient.PrepareBatch();

					var toRemove = new List<string>();
					string[] allLines = File.ReadAllLines(CoinJoinsFilePath);
					foreach (string line in allLines)
					{
						try
						{
							getTxTasks.Add((batch.GetRawTransactionAsync(uint256.Parse(line)), line));
						}
						catch (Exception ex)
						{
							toRemove.Add(line);

							var logEntry = ex is RPCException rpce && rpce.RPCCode == RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY
								? $"CoinJoins file contains invalid transaction ID {line}"
								: $"CoinJoins file got corrupted. Deleting offending line \"{line.Substring(0, 20)}\".";

							Logger.LogWarning($"{logEntry}. {ex.GetType()}: {ex.Message}");
						}
					}

					batch.SendBatchAsync().GetAwaiter().GetResult();

					foreach (var task in getTxTasks)
					{
						try
						{
							var tx = task.txTask.GetAwaiter().GetResult();
							CoinJoins.Add(tx.GetHash());
						}
						catch (Exception ex)
						{
							toRemove.Add(task.line);

							var logEntry = ex is RPCException rpce && rpce.RPCCode == RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY
								? $"CoinJoins file contains invalid transaction ID {task.line}"
								: $"CoinJoins file got corrupted. Deleting offending line \"{task.line.Substring(0, 20)}\".";

							Logger.LogWarning($"{logEntry}. {ex.GetType()}: {ex.Message}");
						}
					}

					if (toRemove.Count != 0) // a little performance boost, it'll be empty almost always
					{
						var newAllLines = allLines.Where(x => !toRemove.Contains(x));
						File.WriteAllLines(CoinJoinsFilePath, newAllLines);
					}
				}
				catch (Exception ex)
				{
					Logger.LogWarning($"CoinJoins file got corrupted. Deleting {CoinJoinsFilePath}. {ex.GetType()}: {ex.Message}");
					File.Delete(CoinJoinsFilePath);
				}
			}

			try
			{
				string roundCountFilePath = Path.Combine(folderPath, "RoundCount.txt");
				if (File.Exists(roundCountFilePath))
				{
					string roundCount = File.ReadAllText(roundCountFilePath);
					CoordinatorRound.RoundCount = long.Parse(roundCount);
				}
				else
				{
					// First time initializes (so the first constructor will increment it and we'll start from 1.)
					CoordinatorRound.RoundCount = 0;
				}
			}
			catch (Exception ex)
			{
				CoordinatorRound.RoundCount = 0;
				Logger.LogInfo($"{nameof(CoordinatorRound.RoundCount)} file was corrupt. Resetting to 0.");
				Logger.LogDebug(ex);
			}

			TrustedNodeNotifyingBehavior.Block += TrustedNodeNotifyingBehavior_BlockAsync;
		}

		private async void TrustedNodeNotifyingBehavior_BlockAsync(object sender, Block block)
		{
			try
			{
				foreach (Transaction tx in block.Transactions)
				{
					await ProcessTransactionAsync(tx).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		public async Task ProcessTransactionAsync(Transaction tx)
		{
			// This should not be needed until we would only accept unconfirmed CJ outputs an no other unconf outs. But it'll be more bulletproof for future extensions.
			// Turns out you shouldn't accept RBF at all never. (See below.)

			// https://github.com/zkSNACKs/WalletWasabi/issues/145
			// if it spends a banned output AND it's not CJ output
			// ban all the outputs of the transaction

			if (RoundConfig.DosSeverity <= 1)
			{
				return;
			}

			foreach (TxIn input in tx.Inputs)
			{
				OutPoint prevOut = input.PrevOut;

				// if coin is not banned
				var foundElem = await UtxoReferee.TryGetBannedAsync(prevOut, notedToo: true).ConfigureAwait(false);
				if (foundElem != null)
				{
					if (!AnyRunningRoundContainsInput(prevOut, out _))
					{
						int newSeverity = foundElem.Severity + 1;
						await UtxoReferee.UnbanAsync(prevOut).ConfigureAwait(false); // since it's not an UTXO anymore

						if (RoundConfig.DosSeverity >= newSeverity)
						{
							var txCoins = tx.Outputs.AsIndexedOutputs().Select(x => x.ToCoin().Outpoint);
							await UtxoReferee.BanUtxosAsync(newSeverity, foundElem.TimeOfBan, forceNoted: foundElem.IsNoted, foundElem.BannedForRound, txCoins.ToArray()).ConfigureAwait(false);
						}
					}
				}
			}
		}

		public async Task MakeSureTwoRunningRoundsAsync(Money feePerInputs = null, Money feePerOutputs = null)
		{
			using (await RoundsListLock.LockAsync().ConfigureAwait(false))
			{
				int runningRoundCount = Rounds.Count(x => x.Status == CoordinatorRoundStatus.Running);

				int confirmationTarget = await AdjustConfirmationTargetAsync(lockCoinJoins: true).ConfigureAwait(false);

				if (runningRoundCount == 0)
				{
					var round = new CoordinatorRound(RpcClient, UtxoReferee, RoundConfig, confirmationTarget, RoundConfig.ConfirmationTarget, RoundConfig.ConfirmationTargetReductionRate);
					round.CoinJoinBroadcasted += Round_CoinJoinBroadcasted;
					round.StatusChanged += Round_StatusChangedAsync;
					await round.ExecuteNextPhaseAsync(RoundPhase.InputRegistration, feePerInputs, feePerOutputs).ConfigureAwait(false);
					Rounds.Add(round);

					var round2 = new CoordinatorRound(RpcClient, UtxoReferee, RoundConfig, confirmationTarget, RoundConfig.ConfirmationTarget, RoundConfig.ConfirmationTargetReductionRate);
					round2.StatusChanged += Round_StatusChangedAsync;
					round2.CoinJoinBroadcasted += Round_CoinJoinBroadcasted;
					await round2.ExecuteNextPhaseAsync(RoundPhase.InputRegistration, feePerInputs, feePerOutputs).ConfigureAwait(false);
					Rounds.Add(round2);
				}
				else if (runningRoundCount == 1)
				{
					var round = new CoordinatorRound(RpcClient, UtxoReferee, RoundConfig, confirmationTarget, RoundConfig.ConfirmationTarget, RoundConfig.ConfirmationTargetReductionRate);
					round.StatusChanged += Round_StatusChangedAsync;
					round.CoinJoinBroadcasted += Round_CoinJoinBroadcasted;
					await round.ExecuteNextPhaseAsync(RoundPhase.InputRegistration, feePerInputs, feePerOutputs).ConfigureAwait(false);
					Rounds.Add(round);
				}
			}
		}

		/// <summary>
		/// Depending on the number of unconfirmed coinjoins lower the confirmation target.
		/// https://github.com/zkSNACKs/WalletWasabi/issues/1155
		/// </summary>
		private async Task<int> AdjustConfirmationTargetAsync(bool lockCoinJoins)
		{
			try
			{
				uint256[] mempoolHashes = await RpcClient.GetRawMempoolAsync().ConfigureAwait(false);
				int unconfirmedCoinJoinsCount = 0;
				if (lockCoinJoins)
				{
					using (await CoinJoinsLock.LockAsync().ConfigureAwait(false))
					{
						unconfirmedCoinJoinsCount = CoinJoins.Intersect(mempoolHashes).Count();
					}
				}
				else
				{
					unconfirmedCoinJoinsCount = CoinJoins.Intersect(mempoolHashes).Count();
				}

				int confirmationTarget = CoordinatorRound.AdjustConfirmationTarget(unconfirmedCoinJoinsCount, RoundConfig.ConfirmationTarget, RoundConfig.ConfirmationTargetReductionRate);
				return confirmationTarget;
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Adjusting confirmation target failed. Falling back to default, specified in config.");
				Logger.LogWarning(ex);

				return RoundConfig.ConfirmationTarget;
			}
		}

		private void Round_CoinJoinBroadcasted(object sender, Transaction transaction)
		{
			CoinJoinBroadcasted?.Invoke(sender, transaction);
		}

		private async void Round_StatusChangedAsync(object sender, CoordinatorRoundStatus status)
		{
			try
			{
				var round = sender as CoordinatorRound;

				Money feePerInputs = null;
				Money feePerOutputs = null;

				// If success save the coinjoin.
				if (status == CoordinatorRoundStatus.Succeded)
				{
					using (await CoinJoinsLock.LockAsync().ConfigureAwait(false))
					{
						uint256 coinJoinHash = round.SignedCoinJoin.GetHash();
						CoinJoins.Add(coinJoinHash);
						await File.AppendAllLinesAsync(CoinJoinsFilePath, new[] { coinJoinHash.ToString() }).ConfigureAwait(false);

						// When a round succeeded, adjust the denomination as to users still be able to register with the latest round's active output amount.
						IEnumerable<(Money value, int count)> outputs = round.SignedCoinJoin.GetIndistinguishableOutputs(includeSingle: true);
						var bestOutput = outputs.OrderByDescending(x => x.count).FirstOrDefault();
						if (bestOutput != default)
						{
							Money activeOutputAmount = bestOutput.value;

							int currentConfirmationTarget = await AdjustConfirmationTargetAsync(lockCoinJoins: false).ConfigureAwait(false);
							var fees = await CoordinatorRound.CalculateFeesAsync(RpcClient, currentConfirmationTarget).ConfigureAwait(false);
							feePerInputs = fees.feePerInputs;
							feePerOutputs = fees.feePerOutputs;

							Money newDenominationToGetInWithactiveOutputs = activeOutputAmount - (feePerInputs + (2 * feePerOutputs));
							if (newDenominationToGetInWithactiveOutputs < RoundConfig.Denomination)
							{
								if (newDenominationToGetInWithactiveOutputs > Money.Coins(0.01m))
								{
									RoundConfig.Denomination = newDenominationToGetInWithactiveOutputs;
									await RoundConfig.ToFileAsync().ConfigureAwait(false);
								}
							}
						}
					}
				}

				// If aborted in signing phase, then ban Alices that did not sign.
				if (status == CoordinatorRoundStatus.Aborted && round.Phase == RoundPhase.Signing)
				{
					IEnumerable<Alice> alicesDidntSign = round.GetAlicesByNot(AliceState.SignedCoinJoin, syncLock: false);

					CoordinatorRound nextRound = GetCurrentInputRegisterableRoundOrDefault(syncLock: false);

					if (nextRound != null)
					{
						int nextRoundAlicesCount = nextRound.CountAlices(syncLock: false);
						var alicesSignedCount = round.AnonymitySet - alicesDidntSign.Count();

						// New round's anonset should be the number of alices that signed in this round.
						// Except if the number of alices in the next round is already larger.
						var newAnonymitySet = Math.Max(alicesSignedCount, nextRoundAlicesCount);
						// But it cannot be larger than the current anonset of that round.
						newAnonymitySet = Math.Min(newAnonymitySet, nextRound.AnonymitySet);

						// Only change the anonymity set of the next round if new anonset does not equal and newanonset is larger than 1.
						if (nextRound.AnonymitySet != newAnonymitySet && newAnonymitySet > 1)
						{
							nextRound.UpdateAnonymitySet(newAnonymitySet, syncLock: false);

							if (nextRoundAlicesCount >= nextRound.AnonymitySet)
							{
								// Progress to the next phase, which will be OutputRegistration
								await nextRound.ExecuteNextPhaseAsync(RoundPhase.ConnectionConfirmation).ConfigureAwait(false);
							}
						}
					}

					foreach (Alice alice in alicesDidntSign) // Because the event sometimes is raised from inside the lock.
					{
						// If it is from any coinjoin, then do not ban.
						IEnumerable<OutPoint> utxosToBan = alice.Inputs.Select(x => x.Outpoint);
						await UtxoReferee.BanUtxosAsync(1, DateTimeOffset.UtcNow, forceNoted: false, round.RoundId, utxosToBan.ToArray()).ConfigureAwait(false);
					}
				}

				// If finished start a new round.
				if (status == CoordinatorRoundStatus.Aborted || status == CoordinatorRoundStatus.Succeded)
				{
					round.StatusChanged -= Round_StatusChangedAsync;
					round.CoinJoinBroadcasted -= Round_CoinJoinBroadcasted;
					await MakeSureTwoRunningRoundsAsync(feePerInputs, feePerOutputs).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		public void AbortAllRoundsInInputRegistration(string reason, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			using (RoundsListLock.Lock())
			{
				foreach (var r in Rounds.Where(x => x.Status == CoordinatorRoundStatus.Running && x.Phase == RoundPhase.InputRegistration))
				{
					r.Abort(reason, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
				}
			}
		}

		public IEnumerable<CoordinatorRound> GetRunningRounds()
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.Where(x => x.Status == CoordinatorRoundStatus.Running).ToArray();
			}
		}

		public CoordinatorRound GetCurrentInputRegisterableRoundOrDefault(bool syncLock = true)
		{
			if (syncLock)
			{
				using (RoundsListLock.Lock())
				{
					return Rounds.FirstOrDefault(x => x.Status == CoordinatorRoundStatus.Running && x.Phase == RoundPhase.InputRegistration);
				}
			}

			return Rounds.FirstOrDefault(x => x.Status == CoordinatorRoundStatus.Running && x.Phase == RoundPhase.InputRegistration);
		}

		public CoordinatorRound TryGetRound(long roundId)
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.SingleOrDefault(x => x.RoundId == roundId);
			}
		}

		public bool AnyRunningRoundContainsInput(OutPoint input, out List<Alice> alices)
		{
			using (RoundsListLock.Lock())
			{
				alices = new List<Alice>();
				foreach (var round in Rounds.Where(x => x.Status == CoordinatorRoundStatus.Running))
				{
					if (round.ContainsInput(input, out List<Alice> roundAlices))
					{
						foreach (var alice in roundAlices)
						{
							alices.Add(alice);
						}
					}
				}
				return alices.Count > 0;
			}
		}

		public async Task<bool> ContainsCoinJoinAsync(uint256 hash)
		{
			using (await CoinJoinsLock.LockAsync().ConfigureAwait(false))
			{
				return CoinJoins.Contains(hash);
			}
		}

		public int GetCoinJoinCount()
		{
			return CoinJoins.Count;
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					using (RoundsListLock.Lock())
					{
						if (TrustedNodeNotifyingBehavior != null)
						{
							TrustedNodeNotifyingBehavior.Block -= TrustedNodeNotifyingBehavior_BlockAsync;
						}

						foreach (CoordinatorRound round in Rounds)
						{
							round.StatusChanged -= Round_StatusChangedAsync;
							round.CoinJoinBroadcasted -= Round_CoinJoinBroadcasted;
						}

						try
						{
							string roundCountFilePath = Path.Combine(FolderPath, "RoundCount.txt");

							IoHelpers.EnsureContainingDirectoryExists(roundCountFilePath);
							File.WriteAllText(roundCountFilePath, CoordinatorRound.RoundCount.ToString());
						}
						catch (Exception ex)
						{
							Logger.LogDebug(ex);
						}
					}
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
}
