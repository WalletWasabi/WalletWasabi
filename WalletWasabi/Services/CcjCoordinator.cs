using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Services
{
	public class CcjCoordinator : IDisposable
	{
		private List<CcjRound> Rounds { get; }
		private AsyncLock RoundsListLock { get; }

		private List<uint256> CoinJoins { get; }
		public string CoinJoinsFilePath => Path.Combine(FolderPath, $"CoinJoins{Network}.txt");
		private AsyncLock CoinJoinsLock { get; }

		public event EventHandler<Transaction> CoinJoinBroadcasted;

		public RPCClient RpcClient { get; }

		public CcjRoundConfig RoundConfig { get; private set; }

		public Network Network { get; }

		public TrustedNodeNotifyingBehavior TrustedNodeNotifyingBehavior { get; }

		public string FolderPath { get; }

		public UtxoReferee UtxoReferee { get; }

		public CcjCoordinator(Network network, TrustedNodeNotifyingBehavior trustedNodeNotifyingBehavior, string folderPath, RPCClient rpc, CcjRoundConfig roundConfig)
		{
			Network = Guard.NotNull(nameof(network), network);
			TrustedNodeNotifyingBehavior = Guard.NotNull(nameof(trustedNodeNotifyingBehavior), trustedNodeNotifyingBehavior);
			FolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(folderPath), folderPath, trim: true);
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);

			Rounds = new List<CcjRound>();
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

							Logger.LogWarning<CcjCoordinator>($"{logEntry}. {ex.GetType()}: {ex.Message}");
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

							Logger.LogWarning<CcjCoordinator>($"{logEntry}. {ex.GetType()}: {ex.Message}");
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
					Logger.LogWarning<CcjCoordinator>($"CoinJoins file got corrupted. Deleting {CoinJoinsFilePath}. {ex.GetType()}: {ex.Message}");
					File.Delete(CoinJoinsFilePath);
				}
			}

			try
			{
				string roundCountFilePath = Path.Combine(folderPath, "RoundCount.txt");
				if (File.Exists(roundCountFilePath))
				{
					string roundCount = File.ReadAllText(roundCountFilePath);
					CcjRound.RoundCount = long.Parse(roundCount);
				}
				else
				{
					// First time initializes (so the first constructor will increment it and we'll start from 1.)
					CcjRound.RoundCount = 0;
				}
			}
			catch (Exception ex)
			{
				CcjRound.RoundCount = 0;
				Logger.LogInfo<CcjCoordinator>($"{nameof(CcjRound.RoundCount)} file was corrupt. Resetting to 0.");
				Logger.LogDebug<CcjCoordinator>(ex);
			}

			TrustedNodeNotifyingBehavior.Block += TrustedNodeNotifyingBehavior_BlockAsync;
		}

		private async void TrustedNodeNotifyingBehavior_BlockAsync(object sender, Block block)
		{
			try
			{
				foreach (Transaction tx in block.Transactions)
				{
					await ProcessTransactionAsync(tx);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<CcjCoordinator>(ex);
			}
		}

		public async Task ProcessTransactionAsync(Transaction tx)
		{
			// This should not be needed until we would only accept unconfirmed CJ outputs an no other unconf outs. But it'll be more bulletproof for future extensions.
			// Turns out you shouldn't accept RBF at all never. (See below.)

			// https://github.com/zkSNACKs/WalletWasabi/issues/145
			//   if a it spends a banned output AND it's not CJ output
			//     ban all the outputs of the transaction

			if (RoundConfig.DosSeverity <= 1)
			{
				return;
			}

			var txId = tx.GetHash();

			foreach (TxIn input in tx.Inputs)
			{
				OutPoint prevOut = input.PrevOut;

				// if coin is not banned
				var foundElem = await UtxoReferee.TryGetBannedAsync(prevOut, notedToo: true);
				if (foundElem != null)
				{
					if (!AnyRunningRoundContainsInput(prevOut, out _))
					{
						int newSeverity = foundElem.Severity + 1;
						await UtxoReferee.UnbanAsync(prevOut); // since it's not an UTXO anymore

						if (RoundConfig.DosSeverity >= newSeverity)
						{
							var txCoins = tx.Outputs.AsIndexedOutputs().Select(x => x.ToCoin().Outpoint);
							await UtxoReferee.BanUtxosAsync(newSeverity, foundElem.TimeOfBan, forceNoted: foundElem.IsNoted, foundElem.BannedForRound, txCoins.ToArray());
						}
					}
				}
			}
		}

		public void UpdateRoundConfig(CcjRoundConfig roundConfig)
		{
			RoundConfig.UpdateOrDefault(roundConfig);
		}

		public async Task MakeSureTwoRunningRoundsAsync(Money feePerInputs = null, Money feePerOutputs = null)
		{
			using (await RoundsListLock.LockAsync())
			{
				int runningRoundCount = Rounds.Count(x => x.Status == CcjRoundStatus.Running);

				int confirmationTarget = await AdjustConfirmationTargetAsync(lockCoinJoins: true);

				if (runningRoundCount == 0)
				{
					var round = new CcjRound(RpcClient, UtxoReferee, RoundConfig, confirmationTarget, RoundConfig.ConfirmationTarget.Value, RoundConfig.ConfirmationTargetReductionRate.Value);
					round.CoinJoinBroadcasted += Round_CoinJoinBroadcasted;
					round.StatusChanged += Round_StatusChangedAsync;
					await round.ExecuteNextPhaseAsync(CcjRoundPhase.InputRegistration, feePerInputs, feePerOutputs);
					Rounds.Add(round);

					var round2 = new CcjRound(RpcClient, UtxoReferee, RoundConfig, confirmationTarget, RoundConfig.ConfirmationTarget.Value, RoundConfig.ConfirmationTargetReductionRate.Value);
					round2.StatusChanged += Round_StatusChangedAsync;
					round2.CoinJoinBroadcasted += Round_CoinJoinBroadcasted;
					await round2.ExecuteNextPhaseAsync(CcjRoundPhase.InputRegistration, feePerInputs, feePerOutputs);
					Rounds.Add(round2);
				}
				else if (runningRoundCount == 1)
				{
					var round = new CcjRound(RpcClient, UtxoReferee, RoundConfig, confirmationTarget, RoundConfig.ConfirmationTarget.Value, RoundConfig.ConfirmationTargetReductionRate.Value);
					round.StatusChanged += Round_StatusChangedAsync;
					round.CoinJoinBroadcasted += Round_CoinJoinBroadcasted;
					await round.ExecuteNextPhaseAsync(CcjRoundPhase.InputRegistration, feePerInputs, feePerOutputs);
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
				uint256[] mempoolHashes = await RpcClient.GetRawMempoolAsync();
				int unconfirmedCoinJoinsCount = 0;
				if (lockCoinJoins)
				{
					using (await CoinJoinsLock.LockAsync())
					{
						unconfirmedCoinJoinsCount = CoinJoins.Intersect(mempoolHashes).Count();
					}
				}
				else
				{
					unconfirmedCoinJoinsCount = CoinJoins.Intersect(mempoolHashes).Count();
				}

				int confirmationTarget = CcjRound.AdjustConfirmationTarget(unconfirmedCoinJoinsCount, RoundConfig.ConfirmationTarget.Value, RoundConfig.ConfirmationTargetReductionRate.Value);
				return confirmationTarget;
			}
			catch (Exception ex)
			{
				Logger.LogWarning<CcjCoordinator>("Adjusting confirmation target failed. Falling back to default, specified in config.");
				Logger.LogWarning<CcjCoordinator>(ex);

				return RoundConfig.ConfirmationTarget.Value;
			}
		}

		private void Round_CoinJoinBroadcasted(object sender, Transaction transaction)
		{
			CoinJoinBroadcasted?.Invoke(sender, transaction);
		}

		private async void Round_StatusChangedAsync(object sender, CcjRoundStatus status)
		{
			try
			{
				var round = sender as CcjRound;

				Money feePerInputs = null;
				Money feePerOutputs = null;

				// If success save the coinjoin.
				if (status == CcjRoundStatus.Succeded)
				{
					using (await CoinJoinsLock.LockAsync())
					{
						uint256 coinJoinHash = round.SignedCoinJoin.GetHash();
						CoinJoins.Add(coinJoinHash);
						await File.AppendAllLinesAsync(CoinJoinsFilePath, new[] { coinJoinHash.ToString() });

						// When a round succeeded, adjust the denomination as to users still be able to register with the latest round's active output amount.
						IEnumerable<(Money value, int count)> outputs = round.SignedCoinJoin.GetIndistinguishableOutputs(includeSingle: true);
						var bestOutput = outputs.OrderByDescending(x => x.count).FirstOrDefault();
						if (bestOutput != default)
						{
							Money activeOutputAmount = bestOutput.value;

							int currentConfirmationTarget = await AdjustConfirmationTargetAsync(lockCoinJoins: false);
							var fees = await CcjRound.CalculateFeesAsync(RpcClient, currentConfirmationTarget);
							feePerInputs = fees.feePerInputs;
							feePerOutputs = fees.feePerOutputs;

							Money newDenominationToGetInWithactiveOutputs = activeOutputAmount - (feePerInputs + (2 * feePerOutputs));
							if (newDenominationToGetInWithactiveOutputs < RoundConfig.Denomination)
							{
								if (newDenominationToGetInWithactiveOutputs > Money.Coins(0.01m))
								{
									RoundConfig.Denomination = newDenominationToGetInWithactiveOutputs;
									await RoundConfig.ToFileAsync();
								}
							}
						}
					}
				}

				// If aborted in signing phase, then ban Alices those did not sign.
				if (status == CcjRoundStatus.Aborted && round.Phase == CcjRoundPhase.Signing)
				{
					IEnumerable<Alice> alicesDidntSign = round.GetAlicesByNot(AliceState.SignedCoinJoin, syncLock: false);

					CcjRound nextRound = GetCurrentInputRegisterableRoundOrDefault(syncLock: false);

					if (nextRound != null)
					{
						int nextRoundAlicesCount = nextRound.CountAlices(syncLock: false);
						var alicesSignedCount = round.AnonymitySet - alicesDidntSign.Count();

						// New round's anonset should be the number of alices those signed in this round.
						// Except if the number of alices in the next round is already larger.
						var newAnonymitySet = Math.Max(alicesSignedCount, nextRoundAlicesCount);
						// But it cannot be larger than the current anonset of that round.
						newAnonymitySet = Math.Min(newAnonymitySet, nextRound.AnonymitySet);

						// Only change the anonymity set of the next round if new anonset doesnt equal and newanonset larger than 1.
						if (nextRound.AnonymitySet != newAnonymitySet && newAnonymitySet > 1)
						{
							nextRound.UpdateAnonymitySet(newAnonymitySet, syncLock: false);

							if (nextRoundAlicesCount >= nextRound.AnonymitySet)
							{
								// Progress to the next phase, which will be OutputRegistration
								await nextRound.ExecuteNextPhaseAsync(CcjRoundPhase.ConnectionConfirmation);
							}
						}
					}

					foreach (Alice alice in alicesDidntSign) // Because the event sometimes is raised from inside the lock.
					{
						// If its from any coinjoin, then do not ban.
						IEnumerable<OutPoint> utxosToBan = alice.Inputs.Select(x => x.Outpoint);
						await UtxoReferee.BanUtxosAsync(1, DateTimeOffset.UtcNow, forceNoted: false, round.RoundId, utxosToBan.ToArray());
					}
				}

				// If finished start a new round.
				if (status == CcjRoundStatus.Aborted || status == CcjRoundStatus.Succeded)
				{
					round.StatusChanged -= Round_StatusChangedAsync;
					round.CoinJoinBroadcasted -= Round_CoinJoinBroadcasted;
					await MakeSureTwoRunningRoundsAsync(feePerInputs, feePerOutputs);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<CcjCoordinator>(ex);
			}
		}

		public void AbortAllRoundsInInputRegistration(string loggingCategory, string reason)
		{
			string category = string.IsNullOrWhiteSpace(loggingCategory) ? nameof(CcjCoordinator) : loggingCategory;
			using (RoundsListLock.Lock())
			{
				foreach (var r in Rounds.Where(x => x.Status == CcjRoundStatus.Running && x.Phase == CcjRoundPhase.InputRegistration))
				{
					r.Abort(category, reason);
				}
			}
		}

		public IEnumerable<CcjRound> GetRunningRounds()
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.Where(x => x.Status == CcjRoundStatus.Running).ToArray();
			}
		}

		public CcjRound GetCurrentInputRegisterableRoundOrDefault(bool syncLock = true)
		{
			if (syncLock)
			{
				using (RoundsListLock.Lock())
				{
					return Rounds.FirstOrDefault(x => x.Status == CcjRoundStatus.Running && x.Phase == CcjRoundPhase.InputRegistration);
				}
			}

			return Rounds.FirstOrDefault(x => x.Status == CcjRoundStatus.Running && x.Phase == CcjRoundPhase.InputRegistration);
		}

		public CcjRound TryGetRound(long roundId)
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
				foreach (var round in Rounds.Where(x => x.Status == CcjRoundStatus.Running))
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
			using (await CoinJoinsLock.LockAsync())
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

						foreach (CcjRound round in Rounds)
						{
							round.StatusChanged -= Round_StatusChangedAsync;
							round.CoinJoinBroadcasted -= Round_CoinJoinBroadcasted;
						}

						try
						{
							string roundCountFilePath = Path.Combine(FolderPath, "RoundCount.txt");

							IoHelpers.EnsureContainingDirectoryExists(roundCountFilePath);
							File.WriteAllText(roundCountFilePath, CcjRound.RoundCount.ToString());
						}
						catch (Exception ex)
						{
							Logger.LogDebug<CcjCoordinator>(ex);
						}
					}
				}

				_disposedValue = true;
			}
		}

		// ~CcjCoordinator() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

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
