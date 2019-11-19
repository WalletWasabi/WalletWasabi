using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions
{
	public class TransactionProcessor
	{
		public static object Lock { get; } = new object();
		public AllTransactionStore TransactionStore { get; }

		public KeyManager KeyManager { get; }

		public CoinsRegistry Coins { get; }
		public Money DustThreshold { get; }

		public event EventHandler<SmartCoin> CoinSpent;

		public event EventHandler<SmartCoin> SpenderConfirmed;

		public event EventHandler<SmartCoin> CoinReceived;

		/// <summary>
		/// Received a confirmed double spend transaction.
		/// </summary>
		public event EventHandler<DoubleSpendReceivedEventArgs> DoubleSpendReceived;

		public event EventHandler<ReplaceTransactionReceivedEventArgs> ReplaceTransactionReceived;

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

		public bool Process(SmartTransaction tx)
		{
			if (!tx.Transaction.PossiblyP2WPKHInvolved())
			{
				return false; // We do not care about non-witness transactions for other than mempool cleanup.
			}

			uint256 txId = tx.GetHash();
			var walletRelevant = false;

			if (tx.Confirmed)
			{
				foreach (var coin in Coins.AsAllCoinsView().CreatedBy(txId))
				{
					coin.Height = tx.Height;
					walletRelevant = true; // relevant
				}

				if (walletRelevant)
				{
					TransactionStore.AddOrUpdate(tx);
				}
			}

			if (!tx.Transaction.IsCoinBase && !walletRelevant) // Transactions we already have and processed would be "double spends" but they shouldn't.
			{
				var doubleSpends = new List<SmartCoin>();
				foreach (SmartCoin coin in Coins.AsAllCoinsView())
				{
					var spent = false;
					foreach (TxoRef spentOutput in coin.SpentOutputs)
					{
						foreach (TxIn txIn in tx.Transaction.Inputs)
						{
							if (spentOutput.TransactionId == txIn.PrevOut.Hash && spentOutput.Index == txIn.PrevOut.N) // Do not do (spentOutput == txIn.PrevOut), it's faster this way, because it won't check for null.
							{
								doubleSpends.Add(coin);
								spent = true;
								walletRelevant = true;
								break;
							}
						}
						if (spent)
						{
							break;
						}
					}
				}

				if (doubleSpends.Any())
				{
					if (tx.Height == Height.Mempool)
					{
						// if the received transaction is spending at least one input already
						// spent by a previous unconfirmed transaction signaling RBF then it is not a double
						// spanding transaction but a replacement transaction.
						var isReplacemenetTx = doubleSpends.Any(x => x.IsReplaceable && !x.Confirmed);
						if (isReplacemenetTx)
						{
							// Undo the replaced transaction by removing the coins it created (if other coin
							// spends it, remove that too and so on) and restoring those that it destroyed
							// ones. After undoing the replaced transaction it will process the replacement
							// transaction.
							var replacedTxId = doubleSpends.First().TransactionId;
							var (destroyed, restored) = Coins.Undo(replacedTxId);

							ReplaceTransactionReceived?.Invoke(this, new ReplaceTransactionReceivedEventArgs(tx, destroyed, restored));

							tx.SetReplacement();
							walletRelevant = true;
						}
						else
						{
							DoubleSpendReceived?.Invoke(this, new DoubleSpendReceivedEventArgs(tx, Enumerable.Empty<SmartCoin>()));
							return false;
						}
					}
					else // new confirmation always enjoys priority
					{
						// remove double spent coins recursively (if other coin spends it, remove that too and so on), will add later if they came to our keys
						foreach (SmartCoin doubleSpentCoin in doubleSpends)
						{
							Coins.Remove(doubleSpentCoin);
						}

						DoubleSpendReceived?.Invoke(this, new DoubleSpendReceivedEventArgs(tx, doubleSpends));

						var unconfirmedDoubleSpentTxId = doubleSpends.First().TransactionId;
						TransactionStore.MempoolStore.TryRemove(unconfirmedDoubleSpentTxId, out _);
						walletRelevant = true;
					}
				}
			}

			var isLikelyCoinJoinOutput = false;
			bool hasEqualOutputs = tx.Transaction.GetIndistinguishableOutputs(includeSingle: false).FirstOrDefault() != default;
			if (hasEqualOutputs)
			{
				var receiveKeys = KeyManager.GetKeys(x => tx.Transaction.Outputs.Any(y => y.ScriptPubKey == x.P2wpkhScript));
				bool allReceivedInternal = receiveKeys.All(x => x.IsInternal);
				if (allReceivedInternal)
				{
					// It is likely a coinjoin if the diff between receive and sent amount is small and have at least 2 equal outputs.
					Money spentAmount = Coins.OutPoints(tx.Transaction.Inputs.ToTxoRefs()).TotalAmount();
					Money receivedAmount = tx.Transaction.Outputs.Where(x => receiveKeys.Any(y => y.P2wpkhScript == x.ScriptPubKey)).Sum(x => x.Value);
					bool receivedAlmostAsMuchAsSpent = spentAmount.Almost(receivedAmount, Money.Coins(0.005m));

					if (receivedAlmostAsMuchAsSpent)
					{
						isLikelyCoinJoinOutput = true;
					}
				}
			}

			List<SmartCoin> newCoins = new List<SmartCoin>();
			List<SmartCoin> spentOwnCoins = null;
			for (var i = 0U; i < tx.Transaction.Outputs.Count; i++)
			{
				// If transaction received to any of the wallet keys:
				var output = tx.Transaction.Outputs[i];
				HdPubKey foundKey = KeyManager.GetKeyForScriptPubKey(output.ScriptPubKey);
				if (foundKey != default)
				{
					walletRelevant = true;

					if (output.Value <= DustThreshold)
					{
						continue;
					}

					foundKey.SetKeyState(KeyState.Used, KeyManager);
					spentOwnCoins ??= Coins.OutPoints(tx.Transaction.Inputs.ToTxoRefs()).ToList();
					var anonset = tx.Transaction.GetAnonymitySet(i);
					if (spentOwnCoins.Count != 0)
					{
						anonset += spentOwnCoins.Min(x => x.AnonymitySet) - 1; // Minus 1, because do not count own.
					}

					SmartCoin newCoin = new SmartCoin(txId, i, output.ScriptPubKey, output.Value, tx.Transaction.Inputs.ToTxoRefs().ToArray(), tx.Height, tx.IsRBF, anonset, isLikelyCoinJoinOutput, foundKey.Label, spenderTransactionId: null, false, pubKey: foundKey); // Do not inherit locked status from key, that's different.
																																																																		   // If we did not have it.
					if (Coins.TryAdd(newCoin))
					{
						newCoins.Add(newCoin);

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
							if (oldCoin != null) // Just to be sure, it is a concurrent collection.
							{
								oldCoin.Height = newCoin.Height;
							}
						}
					}
				}
			}

			// If spends any of our coin
			for (var i = 0; i < tx.Transaction.Inputs.Count; i++)
			{
				var input = tx.Transaction.Inputs[i];

				var foundCoin = Coins.GetByOutPoint(input.PrevOut);
				if (foundCoin != null)
				{
					walletRelevant = true;
					var alreadyKnown = foundCoin.SpenderTransactionId == txId;
					foundCoin.SpenderTransactionId = txId;

					if (!alreadyKnown)
					{
						Coins.Spend(foundCoin);
						CoinSpent?.Invoke(this, foundCoin);
					}

					if (tx.Confirmed)
					{
						SpenderConfirmed?.Invoke(this, foundCoin);
					}
				}
			}

			if (walletRelevant)
			{
				TransactionStore.AddOrUpdate(tx);
			}

			foreach (var newCoin in newCoins)
			{
				CoinReceived?.Invoke(this, newCoin);
			}

			return walletRelevant;
		}

		public void UndoBlock(Height blockHeight)
		{
			Coins.SwitchToUnconfirmFromBlock(blockHeight);
		}
	}
}
