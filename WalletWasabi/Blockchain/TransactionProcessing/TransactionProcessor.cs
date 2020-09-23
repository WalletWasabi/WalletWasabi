using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionProcessing
{
	public class TransactionProcessor
	{
		public TransactionProcessor(
			AllTransactionStore transactionStore,
			KeyManager keyManager,
			Money dustThreshold,
			int privacyLevelThreshold = 100)
		{
			TransactionStore = Guard.NotNull(nameof(transactionStore), transactionStore);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			DustThreshold = Guard.NotNull(nameof(dustThreshold), dustThreshold);
			Coins = new CoinsRegistry(privacyLevelThreshold);
		}

		public event EventHandler<ProcessedResult> WalletRelevantTransactionProcessed;

		private static object Lock { get; } = new object();
		public AllTransactionStore TransactionStore { get; }

		public KeyManager KeyManager { get; }

		public CoinsRegistry Coins { get; }
		public Money DustThreshold { get; }

		#region Progress

		public int QueuedTxCount { get; private set; }
		public int QueuedProcessedTxCount { get; private set; }

		#endregion Progress

		public IEnumerable<ProcessedResult> Process(IEnumerable<SmartTransaction> txs)
		{
			var rets = new List<ProcessedResult>();

			lock (Lock)
			{
				try
				{
					QueuedTxCount = txs.Count();
					foreach (var tx in txs)
					{
						rets.Add(ProcessNoLock(tx));
						QueuedProcessedTxCount++;
					}
				}
				finally
				{
					QueuedTxCount = 0;
					QueuedProcessedTxCount = 0;
				}
			}

			foreach (var ret in rets.Where(x => x.IsNews))
			{
				WalletRelevantTransactionProcessed?.Invoke(this, ret);
			}

			return rets;
		}

		public IEnumerable<ProcessedResult> Process(params SmartTransaction[] txs)
			=> Process(txs as IEnumerable<SmartTransaction>);

		public ProcessedResult Process(SmartTransaction tx)
		{
			ProcessedResult ret;
			lock (Lock)
			{
				try
				{
					QueuedTxCount = 1;
					ret = ProcessNoLock(tx);
				}
				finally
				{
					QueuedTxCount = 0;
				}
			}
			if (ret.IsNews)
			{
				WalletRelevantTransactionProcessed?.Invoke(this, ret);
			}
			return ret;
		}

		private ProcessedResult ProcessNoLock(SmartTransaction tx)
		{
			var result = new ProcessedResult(tx);

			// We do not care about non-witness transactions for other than mempool cleanup.
			if (tx.Transaction.PossiblyP2WPKHInvolved())
			{
				uint256 txId = tx.GetHash();

				// Performance ToDo: txids could be cached in a hashset here by the AllCoinsView and then the contains would be fast.
				if (!tx.Transaction.IsCoinBase && !Coins.AsAllCoinsView().CreatedBy(txId).Any()) // Transactions we already have and processed would be "double spends" but they shouldn't.
				{
					var doubleSpends = new List<SmartCoin>();
					foreach (var txin in tx.Transaction.Inputs)
					{
						if (Coins.TryGetSpenderSmartCoinsByOutPoint(txin.PrevOut, out var coins))
						{
							doubleSpends.AddRange(coins);
						}
					}

					if (doubleSpends.Any())
					{
						if (tx.Height == Height.Mempool)
						{
							// if the received transaction is spending at least one input already
							// spent by a previous unconfirmed transaction signaling RBF then it is not a double
							// spending transaction but a replacement transaction.
							var isReplacementTx = doubleSpends.Any(x => x.IsReplaceable && !x.Confirmed);
							if (isReplacementTx)
							{
								// Undo the replaced transaction by removing the coins it created (if other coin
								// spends it, remove that too and so on) and restoring those that it replaced.
								// After undoing the replaced transaction it will process the replacement transaction.
								var replacedTxId = doubleSpends.First().TransactionId;
								var (replaced, restored) = Coins.Undo(replacedTxId);

								result.ReplacedCoins.AddRange(replaced);
								result.RestoredCoins.AddRange(restored);

								foreach (var replacedTransactionId in replaced.Select(coin => coin.TransactionId))
								{
									TransactionStore.MempoolStore.TryRemove(replacedTransactionId, out _);
								}

								tx.SetReplacement();
							}
							else
							{
								return result;
							}
						}
						else // new confirmation always enjoys priority
						{
							// remove double spent coins recursively (if other coin spends it, remove that too and so on), will add later if they came to our keys
							foreach (SmartCoin doubleSpentCoin in doubleSpends)
							{
								Coins.Remove(doubleSpentCoin);
							}

							result.SuccessfullyDoubleSpentCoins.AddRange(doubleSpends);

							var unconfirmedDoubleSpentTxId = doubleSpends.First().TransactionId;
							TransactionStore.MempoolStore.TryRemove(unconfirmedDoubleSpentTxId, out _);
						}
					}
				}

				List<SmartCoin> spentOwnCoins = null;
				for (var i = 0U; i < tx.Transaction.Outputs.Count; i++)
				{
					// If transaction received to any of the wallet keys:
					var output = tx.Transaction.Outputs[i];
					HdPubKey foundKey = KeyManager.GetKeyForScriptPubKey(output.ScriptPubKey);
					if (foundKey != default)
					{
						if (!foundKey.IsInternal)
						{
							tx.Label = SmartLabel.Merge(tx.Label, foundKey.Label);
						}

						foundKey.SetKeyState(KeyState.Used, KeyManager);
						if (output.Value <= DustThreshold)
						{
							result.ReceivedDusts.Add(output);
							continue;
						}

						spentOwnCoins ??= Coins.OutPoints(tx.Transaction.Inputs).ToList();

						// Get the anonymity set of i-th output in the transaction.
						var anonset = tx.Transaction.GetAnonymitySet(i);
						// If we provided inputs to the transaction.
						if (spentOwnCoins.Count != 0)
						{
							// Take the input that we provided with the the smallest anonset.
							// And add that to the base anonset from the tx.
							// Our smallest anonset input is the relevant here, because this way the common input ownership heuristic is considered.
							// Take minus 1, because we do not want to count own into the anonset, so...
							// If the anonset of our UTXO would be 1, and the smallest anonset of our inputs would be 1, too, then we don't make...
							// The new UTXO's anonset 2, but only 1.
							anonset += spentOwnCoins.Min(x => x.AnonymitySet) - 1;
						}

						SmartCoin newCoin = new SmartCoin(txId, i, output.ScriptPubKey, output.Value, tx.Transaction.Inputs.ToOutPoints().ToArray(), tx.Height, tx.IsRBF, anonset, foundKey.Label, spenderTransactionId: null, false, pubKey: foundKey); // Do not inherit locked status from key, that's different.

						result.ReceivedCoins.Add(newCoin);
						// If we did not have it.
						if (Coins.TryAdd(newCoin))
						{
							result.NewlyReceivedCoins.Add(newCoin);

							// Make sure there's always 21 clean keys generated and indexed.
							KeyManager.AssertCleanKeysIndexed(isInternal: foundKey.IsInternal);

							if (foundKey.IsInternal)
							{
								// Make sure there's always 14 internal locked keys generated and indexed.
								KeyManager.AssertLockedInternalKeysIndexed(14);
							}
						}
						else // If we had this coin already.
						{
							if (newCoin.Height != Height.Mempool) // Update the height of this old coin we already had.
							{
								SmartCoin oldCoin = Coins.AsAllCoinsView().GetByOutPoint(new OutPoint(txId, i));
								if (oldCoin is { }) // Just to be sure, it is a concurrent collection.
								{
									result.NewlyConfirmedReceivedCoins.Add(newCoin);
									oldCoin.Height = newCoin.Height;
								}
							}
						}
					}
				}

				var isLikelyCj = false;
				if (tx.Transaction.Inputs.Count > 1 // The tx must have more than one input in order to be a coinjoin.
					&& tx.Transaction.HasIndistinguishableOutputs()) // The tx must have more than one equal output in order to be a coinjoin.
				{
					isLikelyCj = true;
				}

				var prevOutSet = tx.Transaction.Inputs.Select(x => x.PrevOut).ToHashSet();
				foreach (var coin in Coins.AsAllCoinsView())
				{
					// If spends any of our coin
					if (prevOutSet.TryGetValue(coin.OutPoint, out OutPoint input))
					{
						var alreadyKnown = coin.SpenderTransactionId == txId;
						coin.SpenderTransactionId = txId;
						result.SpentCoins.Add(coin);

						if (!alreadyKnown)
						{
							Coins.Spend(coin);
							result.NewlySpentCoins.Add(coin);
						}

						if (tx.Confirmed)
						{
							result.NewlyConfirmedSpentCoins.Add(coin);
						}

						if (isLikelyCj)
						{
							result.IsLikelyOwnCoinJoin = true;
						}
					}
				}

				foreach (var coin in result.ReceivedCoins)
				{
					// May be too late to set it at this point. It'd be better to set at the constructor, but couldn't find a way without ruining performance.
					coin.IsLikelyCoinJoinOutput = result.IsLikelyOwnCoinJoin;
				}

				if (result.IsNews)
				{
					TransactionStore.AddOrUpdate(tx);
				}
			}

			return result;
		}

		public void UndoBlock(Height blockHeight)
		{
			Coins.SwitchToUnconfirmFromBlock(blockHeight);
		}
	}
}
