using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class CoinsRegistry : ICoinsView
{
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<uint256, Dictionary<OutPoint, SmartCoin>> CoinsByTransactionsIds { get; } = new();

	/// <summary>
	/// Cache the prevOut of all inputs of transactions that created some coins.
	/// The values of this cache match the keys of CoinsByTransactionsIds.
	/// </summary>
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<OutPoint, uint256> TxIdsByInputsPrevOut { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<HdPubKey, HashSet<SmartCoin>> CoinsByPubKeys { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> LatestCoinsSnapshot { get; set; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> LatestSpentCoinsSnapshot { get; set; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private bool InvalidateSnapshot { get; set; }

	private object Lock { get; } = new();

	private CoinsView AsCoinsViewNoLock()
	{
		UpdateSnapshotsNoLock();
		return new CoinsView(LatestCoinsSnapshot);
	}

	private CoinsView AsCoinsView()
	{
		lock (Lock)
		{
			return AsCoinsViewNoLock();
		}
	}

	private CoinsView AsSpentCoinsViewNoLock()
	{
		UpdateSnapshotsNoLock();
		return new CoinsView(LatestSpentCoinsSnapshot);
	}

	private CoinsView AsSpentCoinsView()
	{
		lock (Lock)
		{
			return AsSpentCoinsViewNoLock();
		}
	}

	private void UpdateSnapshotsNoLock()
	{
		if (!InvalidateSnapshot)
		{
			return;
		}

		var newCoinsSnapshot = new HashSet<SmartCoin>();
		var newSpentCoinsSnapshot = new HashSet<SmartCoin>();
		foreach (var coin in CoinsByTransactionsIds.Values.SelectMany(x => x.Values))
		{
			if (coin.SpenderTransaction is null)
			{
				newCoinsSnapshot.Add(coin);
			}
			else
			{
				newSpentCoinsSnapshot.Add(coin);
			}
		}

		LatestCoinsSnapshot = newCoinsSnapshot;
		LatestSpentCoinsSnapshot = newSpentCoinsSnapshot;
		InvalidateSnapshot = false;
	}

	public bool TryGetByOutPoint(OutPoint outpoint, [NotNullWhen(true)] out SmartCoin? coin) => AsCoinsView().TryGetByOutPoint(outpoint, out coin);

	public bool TryAdd(SmartCoin coin)
	{
		lock (Lock)
		{
			if (!CoinsByPubKeys.TryGetValue(coin.HdPubKey, out HashSet<SmartCoin>? coinsOfPubKey))
			{
				coinsOfPubKey = new HashSet<SmartCoin>();
				CoinsByPubKeys.Add(coin.HdPubKey, coinsOfPubKey);
			}

			coinsOfPubKey.Add(coin);

			if (CoinsByTransactionsIds.TryGetValue(coin.TransactionId, out var coinsByOutpoints))
			{
				return coinsByOutpoints.TryAdd(coin.Outpoint, coin);
			}

			CoinsByTransactionsIds.Add(
				coin.TransactionId,
				new Dictionary<OutPoint, SmartCoin>() { { coin.Outpoint, coin } });

			foreach (var input in coin.Transaction.Transaction.Inputs)
			{
				TxIdsByInputsPrevOut.AddOrReplace(input.PrevOut, coin.TransactionId);
			}

			InvalidateSnapshot = true;
		}

		return true;
	}

	public void Spend(SmartCoin spentCoin, SmartTransaction tx)
	{
		tx.TryAddWalletInput(spentCoin);
		spentCoin.SpenderTransaction = tx;
	}

	public void SwitchToUnconfirmFromBlock(Height blockHeight)
	{
		lock (Lock)
		{
			foreach (var coin in AsCoinsViewNoLock().AtBlockHeight(blockHeight))
			{
				var descendantCoins = DescendantOfAndSelf(coin);
				foreach (var toSwitch in descendantCoins)
				{
					toSwitch.Height = Height.Mempool;
				}
			}
		}
	}

	public bool IsKnown(uint256 txid)
	{
		lock (Lock)
		{
			return CoinsByTransactionsIds.ContainsKey(txid);
		}
	}

	public bool TryGetCoinsByInputPrevOut(OutPoint prevOut, [NotNullWhen(true)] out HashSet<SmartCoin>? coins)
	{
		lock (Lock)
		{
			return TryGetCoinsByInputPrevOutNoLock(prevOut, out coins);
		}
	}

	private bool TryGetCoinsByInputPrevOutNoLock(OutPoint prevOut, [NotNullWhen(true)] out HashSet<SmartCoin>? coins)
	{
		coins = null;
		if (!TxIdsByInputsPrevOut.TryGetValue(prevOut, out var txId))
		{
			return false;
		}

		if (!CoinsByTransactionsIds.TryGetValue(txId, out var coinsByOutpoints))
		{
			return false;
		}

		coins = coinsByOutpoints.Values.ToHashSet();
		return true;
	}

	public bool TryGetCoinByOutPoint(OutPoint outPoint, [NotNullWhen(true)] out SmartCoin? coin)
	{
		coin = null;
		lock (Lock)
		{
			if (!CoinsByTransactionsIds.TryGetValue(outPoint.Hash, out var coinsByOutpoint))
			{
				return false;
			}

			if (!coinsByOutpoint.TryGetValue(outPoint, out coin))
			{
				return false;
			}
		}

		return true;
	}

	internal (ICoinsView toRemove, ICoinsView toAdd) Undo(uint256 txId)
	{
		lock (Lock)
		{
			var allCoins = AsAllCoinsViewNoLock();
			var toRemove = new HashSet<SmartCoin>();
			var toAdd = new HashSet<SmartCoin>();

			// The transactions that needs to be removed are txId and its descendants.
			HashSet<uint256> txIdsToRemove = new () { txId };
			foreach (var myOutput in allCoins.CreatedBy(txId))
			{
				var txIdsToRemoveForOutput = allCoins
					.DescendantOf(myOutput)
					.Select(x => x.TransactionId)
					.Distinct();

				foreach (var txIdToRemove in txIdsToRemoveForOutput)
				{
					txIdsToRemove.Add(txIdToRemove);
				}
			}

			foreach (var txIdToRemove in txIdsToRemove)
			{
				// Remove the transaction from CoinsByTransactionIds cache.
				if (CoinsByTransactionsIds.Remove(txIdToRemove, out var coinsRemovedByOutpoint))
				{
					foreach (var removedCoin in coinsRemovedByOutpoint.Values)
					{
						toRemove.Add(removedCoin);
					}

					// Remove the coins from the CoinsByPubKeys cache.
					foreach (var coinsByPubKey in CoinsByPubKeys.ToList())
					{
						if (coinsByPubKey.Value.Count == coinsByPubKey.Value.RemoveWhere(x => coinsRemovedByOutpoint.ContainsValue(x)))
						{
							// There are no more coins on the PubKey, remove the entry.
							CoinsByPubKeys.Remove(coinsByPubKey.Key);
						}
					}

					// Remove the prevOut of the inputs of the transaction from TxIdsByInputsPrevOut cache.
					// This cache can be really big and it's better to avoid .ToList().
					var keysToRemove = new HashSet<OutPoint>();
					foreach (var removedTxIdByInputPrevOut in TxIdsByInputsPrevOut.Where(x => x.Value.Equals(txIdToRemove)))
					{
						keysToRemove.Add(removedTxIdByInputPrevOut.Key);
					}

					foreach (var keyToRemove in keysToRemove)
					{
						TxIdsByInputsPrevOut.Remove(keyToRemove);
					}
				}

				// The coins that were spent by the removed transaction are now unspent.
				foreach (var coin in allCoins.Where(x => x.SpenderTransaction is not null && x.SpenderTransaction.GetHash() == txIdToRemove))
				{
					coin.SpenderTransaction = null;
					toAdd.Add(coin);
				}
			}

			// Remove from results coins that have been unspent but removed afterwards.
			foreach (var coin in toAdd.ToList().Where(coin => toRemove.Contains(coin)))
			{
				toAdd.Remove(coin);
			}

			InvalidateSnapshot = true;

			return (new CoinsView(toRemove), new CoinsView(toAdd));
		}
	}

	public IReadOnlyList<SmartCoin> GetMyInputs(SmartTransaction transaction)
	{
		var myInputs = new List<SmartCoin>();

		lock (Lock)
		{
			foreach (TxIn input in transaction.Transaction.Inputs)
			{
				if (!CoinsByTransactionsIds.TryGetValue(input.PrevOut.Hash, out var coinsByOutpoints))
				{
					continue;
				}

				if (coinsByOutpoints.TryGetValue(input.PrevOut, out var coin))
				{
					myInputs.Add(coin);
				}
			}
		}

		return myInputs;
	}

	/// <returns><c>true</c> if the coin registry contains at least one unspent coin with <paramref name="hdPubKey"/>, <c>false</c> otherwise.</returns>
	public bool HasUnspentCoin(HdPubKey hdPubKey)
	{
		lock (Lock)
		{
			if (CoinsByPubKeys.TryGetValue(hdPubKey, out HashSet<SmartCoin>? coinsOfPubKey))
			{
				return coinsOfPubKey.Any(coin => !coin.IsSpent());
			}

			return false;
		}
	}

	public bool IsUsed(HdPubKey hdPubKey)
	{
		lock (Lock)
		{
			return CoinsByPubKeys.TryGetValue(hdPubKey, out _);
		}
	}

	public ICoinsView AsAllCoinsView()
	{
		lock (Lock)
		{
			return new CoinsView(AsAllCoinsViewNoLock().ToList());
		}
	}

	private ICoinsView AsAllCoinsViewNoLock() => new CoinsView(AsCoinsViewNoLock().Concat(AsSpentCoinsViewNoLock()).ToList());

	public ICoinsView AtBlockHeight(Height height) => AsCoinsView().AtBlockHeight(height);

	public ICoinsView Available() => AsCoinsView().Available();

	public ICoinsView ChildrenOf(SmartCoin coin) => AsCoinsView().ChildrenOf(coin);

	public ICoinsView CoinJoinInProcess() => AsCoinsView().CoinJoinInProcess();

	public ICoinsView Confirmed() => AsCoinsView().Confirmed();

	public ICoinsView DescendantOf(SmartCoin coin) => AsCoinsView().DescendantOf(coin);

	private ICoinsView DescendantOfAndSelfNoLock(SmartCoin coin) => AsCoinsViewNoLock().DescendantOfAndSelf(coin);

	public ICoinsView DescendantOfAndSelf(SmartCoin coin) => AsCoinsView().DescendantOfAndSelf(coin);

	public ICoinsView FilterBy(Func<SmartCoin, bool> expression) => AsCoinsView().FilterBy(expression);

	public IEnumerator<SmartCoin> GetEnumerator() => AsCoinsView().GetEnumerator();

	public ICoinsView OutPoints(ISet<OutPoint> outPoints) => AsCoinsView().OutPoints(outPoints);

	public ICoinsView OutPoints(TxInList txIns) => AsCoinsView().OutPoints(txIns);

	public ICoinsView CreatedBy(uint256 txid) => AsCoinsView().CreatedBy(txid);

	public ICoinsView SpentBy(uint256 txid) => AsSpentCoinsView().SpentBy(txid);

	public SmartCoin[] ToArray() => AsCoinsView().ToArray();

	public Money TotalAmount() => AsCoinsView().TotalAmount();

	public ICoinsView Unconfirmed() => AsCoinsView().Unconfirmed();

	public ICoinsView Unspent() => AsCoinsView().Unspent();

	IEnumerator IEnumerable.GetEnumerator() => AsCoinsView().GetEnumerator();
}
