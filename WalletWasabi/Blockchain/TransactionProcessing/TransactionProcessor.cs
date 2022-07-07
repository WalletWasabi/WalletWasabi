using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionProcessing;

public class TransactionProcessor
{
	public TransactionProcessor(
		AllTransactionStore transactionStore,
		KeyManager keyManager,
		Money dustThreshold,
		int privacyLevelThreshold)
	{
		TransactionStore = Guard.NotNull(nameof(transactionStore), transactionStore);
		KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
		DustThreshold = Guard.NotNull(nameof(dustThreshold), dustThreshold);
		Coins = new CoinsRegistry();
		BlockchainAnalyzer = new BlockchainAnalyzer(privacyLevelThreshold);
	}

	public event EventHandler<ProcessedResult>? WalletRelevantTransactionProcessed;

	private static object Lock { get; } = new object();
	public AllTransactionStore TransactionStore { get; }
	private HashSet<uint256> Aware { get; } = new();

	public KeyManager KeyManager { get; }

	public CoinsRegistry Coins { get; }
	public BlockchainAnalyzer BlockchainAnalyzer { get; }
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

	/// <summary>
	/// Was the transaction already processed by the transaction processor?
	/// </summary>
	public bool IsAware(uint256 tx)
	{
		lock (Lock)
		{
			return Aware.Contains(tx);
		}
	}

	public ProcessedResult Process(SmartTransaction tx)
	{
		ProcessedResult ret;
		lock (Lock)
		{
			Aware.Add(tx.GetHash());
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
		if (!tx.Transaction.PossiblyP2WPKHInvolved())
		{
			return result;
		}

		uint256 txId = tx.GetHash();

		// If we already have the transaction, then let's work on that.
		if (TransactionStore.TryGetTransaction(txId, out var foundTx))
		{
			foundTx.TryUpdate(tx);
			tx = foundTx;
			result = new ProcessedResult(tx);
		}

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
					var isReplacementTx = doubleSpends.Any(x => x.IsReplaceable());
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
					var unconfirmedDoubleSpentTxId = doubleSpends.First().TransactionId;
					if (TransactionStore.MempoolStore.TryGetTransaction(unconfirmedDoubleSpentTxId, out var replacedTx) && replacedTx.IsReplacement)
					{
						var (replaced, restored) = Coins.Undo(unconfirmedDoubleSpentTxId);

						result.ReplacedCoins.AddRange(replaced);
						result.RestoredCoins.AddRange(restored);

						foreach (var replacedTransactionId in replaced.Select(coin => coin.TransactionId))
						{
							TransactionStore.MempoolStore.TryRemove(replacedTransactionId, out _);
						}
					}
					else
					{
						// remove double spent coins recursively (if other coin spends it, remove that too and so on), will add later if they came to our keys
						foreach (SmartCoin doubleSpentCoin in doubleSpends)
						{
							Coins.Remove(doubleSpentCoin);
						}

						result.SuccessfullyDoubleSpentCoins.AddRange(doubleSpends);

						TransactionStore.MempoolStore.TryRemove(unconfirmedDoubleSpentTxId, out _);
					}
				}
			}
		}

		var myInputs = Coins.AsAllCoinsView().OutPoints(tx.Transaction.Inputs.Select(x => x.PrevOut).ToHashSet()).ToImmutableList();
		for (var i = 0U; i < tx.Transaction.Outputs.Count; i++)
		{
			// If transaction received to any of the wallet keys:
			var output = tx.Transaction.Outputs[i];
			if (KeyManager.TryGetKeyForScriptPubKey(output.ScriptPubKey, out HdPubKey? foundKey))
			{
				if (!foundKey.IsInternal)
				{
					tx.Label = SmartLabel.Merge(tx.Label, foundKey.Label);
				}

				foundKey.SetKeyState(KeyState.Used, KeyManager);
				var areWeSending = myInputs.Any();
				if (output.Value <= DustThreshold && !areWeSending)
				{
					result.ReceivedDusts.Add(output);
					continue;
				}

				SmartCoin newCoin = new(tx, i, foundKey);

				result.ReceivedCoins.Add(newCoin);

				// If we did not have it.
				if (Coins.TryAdd(newCoin))
				{
					result.NewlyReceivedCoins.Add(newCoin);

					// Make sure there's always 21 clean keys generated and indexed.
					KeyManager.AssertCleanKeysIndexed(isInternal: foundKey.IsInternal);
				}
				else // If we had this coin already.
				{
					if (newCoin.Height != Height.Mempool) // Update the height of this old coin we already had.
					{
						if (Coins.AsAllCoinsView().TryGetByOutPoint(new OutPoint(txId, i), out var oldCoin)) // Just to be sure, it is a concurrent collection.
						{
							result.NewlyConfirmedReceivedCoins.Add(newCoin);
							oldCoin.Height = newCoin.Height;
						}
					}
				}
			}
		}

		// If spends any of our coin
		foreach (var coin in myInputs)
		{
			var alreadyKnown = coin.SpenderTransaction == tx;
			result.SpentCoins.Add(coin);
			Coins.Spend(coin, tx);

			if (!alreadyKnown)
			{
				result.NewlySpentCoins.Add(coin);
			}

			if (tx.Confirmed)
			{
				result.NewlyConfirmedSpentCoins.Add(coin);
			}
		}

		if (result.IsNews)
		{
			TransactionStore.AddOrUpdate(tx);
		}

		BlockchainAnalyzer.Analyze(result.Transaction);

		return result;
	}

	public void UndoBlock(Height blockHeight)
	{
		Coins.SwitchToUnconfirmFromBlock(blockHeight);
	}
}
