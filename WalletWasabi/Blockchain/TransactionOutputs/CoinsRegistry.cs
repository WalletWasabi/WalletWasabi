using NBitcoin;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class CoinsRegistry : ICoinsView
{
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> Coins { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<uint256> KnownTransactions { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<OutPoint, SmartCoin> OutpointCoinCache { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> LatestCoinsSnapshot { get; set; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private bool InvalidateSnapshot { get; set; }

	private object Lock { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> SpentCoins { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> LatestSpentCoinsSnapshot { get; set; } = new();

	/// <summary>Maps each outpoint to smart coins (i.e. UTXOs) that exist thanks to the outpoint. The same hash-set (reference) is also stored in <see cref="CoinsByTransactionId"/>.</summary>
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<OutPoint, HashSet<SmartCoin>> CoinsByOutPoint { get; } = new();

	/// <summary>Maps each TXID to smart coins (i.e. UTXOs). The same hash-set (reference) is also stored in <see cref="CoinsByOutPoint"/>.</summary>
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<uint256, HashSet<SmartCoin>> CoinsByTransactionId { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<OutPoint, SmartCoin> SpentCoinsByOutPoint { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<HdPubKey, HashSet<SmartCoin>> CoinsByPubKeys { get; } = new();

	/// <summary>Maps each TXIDs to a balance change that is caused by the corresponding wallet transaction.</summary>
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<uint256, Money> TransactionAmountsByTxid { get; } = new();

	private CoinsView AsCoinsViewNoLock()
	{
		UpdateSnapshotsNoLock();
		return new CoinsView(LatestCoinsSnapshot);
	}

	private CoinsView AsSpentCoinsViewNoLock()
	{
		UpdateSnapshotsNoLock();
		return new CoinsView(LatestSpentCoinsSnapshot);
	}

	private CoinsView AsCoinsView()
	{
		lock (Lock)
		{
			return AsCoinsViewNoLock();
		}
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

		LatestCoinsSnapshot = Coins.ToHashSet();
		LatestSpentCoinsSnapshot = SpentCoins.ToHashSet();
		InvalidateSnapshot = false;
	}

	public bool TryAdd(SmartCoin coin)
	{
		var added = false;
		lock (Lock)
		{
			if (!SpentCoins.Contains(coin))
			{
				added = Coins.Add(coin);
				KnownTransactions.Add(coin.TransactionId);
				OutpointCoinCache.AddOrReplace(coin.Outpoint, coin);

				if (!CoinsByPubKeys.TryGetValue(coin.HdPubKey, out HashSet<SmartCoin>? coinsOfPubKey))
				{
					coinsOfPubKey = new();
					CoinsByPubKeys.Add(coin.HdPubKey, coinsOfPubKey);
				}

				coinsOfPubKey.Add(coin);

				if (added)
				{
					if (!CoinsByTransactionId.TryGetValue(coin.TransactionId, out HashSet<SmartCoin>? hashSet))
					{
						hashSet = new();
						CoinsByTransactionId.Add(coin.TransactionId, hashSet);
					}

					hashSet.Add(coin);

					// Each prevOut of the transaction contributes to the existence of coins.
					foreach (TxIn input in coin.Transaction.Transaction.Inputs)
					{
						CoinsByOutPoint[input.PrevOut] = hashSet;
					}

					InvalidateSnapshot = true;
				}

				UpdateTransactionAmountNoLock(coin.TransactionId, coin.Amount);
			}
		}

		return added;
	}

	private ICoinsView RemoveNoLock(SmartCoin coin)
	{
		var coinsToRemove = DescendantOfAndSelfNoLock(coin);
		foreach (var toRemove in coinsToRemove)
		{
			if (!Coins.Remove(toRemove))
			{
				if (SpentCoins.Remove(toRemove))
				{
					SpentCoinsByOutPoint.Remove(toRemove.Outpoint);
				}
			}

			var removedCoinOutPoint = toRemove.Outpoint;
			OutpointCoinCache.Remove(removedCoinOutPoint);

			if (CoinsByPubKeys.TryGetValue(coin.HdPubKey, out HashSet<SmartCoin>? coinsOfPubKey))
			{
				coinsOfPubKey.Remove(coin);

				if (coinsOfPubKey.Count == 0)
				{
					CoinsByPubKeys.Remove(coin.HdPubKey);
				}
			}
		}

		foreach (var txId in coinsToRemove.Select(x => x.TransactionId).Distinct())
		{
			if (!CoinsByTransactionId.TryGetValue(txId, out var coins))
			{
				continue;
			}

			coins.RemoveWhere(x => coinsToRemove.Contains(x));

			if (coins.Any())
			{
				continue;
			}

			// No more coins were created by this transaction.
			KnownTransactions.Remove(txId);

			if (!CoinsByTransactionId.Remove(txId, out var referenceHashSetRemoved))
			{
				throw new InvalidOperationException($"Failed to remove '{txId}' from {nameof(CoinsByTransactionId)}.");
			}

			foreach (var kvp in CoinsByOutPoint.ToList())
			{
				if (ReferenceEquals(kvp.Value, referenceHashSetRemoved))
				{
					CoinsByOutPoint.Remove(kvp.Key);
				}
			}

			if (!TransactionAmountsByTxid.Remove(txId))
			{
				throw new InvalidOperationException($"Failed to remove '{txId}' from {nameof(TransactionAmountsByTxid)}.");
			}
		}

		InvalidateSnapshot = true;
		return coinsToRemove;
	}

	public void Spend(SmartCoin spentCoin, SmartTransaction tx)
	{
		tx.TryAddWalletInput(spentCoin);
		spentCoin.SpenderTransaction = tx;

		lock (Lock)
		{
			if (Coins.Remove(spentCoin))
			{
				InvalidateSnapshot = true;
				if (SpentCoins.Add(spentCoin))
				{
					SpentCoinsByOutPoint.Add(spentCoin.Outpoint, spentCoin);
				}

				UpdateTransactionAmountNoLock(tx.GetHash(), Money.Zero - spentCoin.Amount);
			}
		}
	}

	private void UpdateTransactionAmountNoLock(uint256 txid, Money diff)
	{
		TransactionAmountsByTxid[txid] = TransactionAmountsByTxid.TryGetValue(txid, out Money? current) ? current + diff : diff;
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
			return KnownTransactions.Contains(txid);
		}
	}

	public bool TryGetByOutPoint(OutPoint outpoint, [NotNullWhen(true)] out SmartCoin? coin)
	{
		lock (Lock)
		{
			return OutpointCoinCache.TryGetValue(outpoint, out coin);
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
		return CoinsByOutPoint.TryGetValue(prevOut, out coins);
	}

	public bool TryGetSpentCoinByOutPoint(OutPoint outPoint, [NotNullWhen(true)] out SmartCoin? coin)
	{
		lock (Lock)
		{
			return SpentCoinsByOutPoint.TryGetValue(outPoint, out coin);
		}
	}

	internal (ICoinsView toRemove, ICoinsView toAdd) Undo(uint256 txId)
	{
		lock (Lock)
		{
			var allCoins = AsAllCoinsViewNoLock();
			var toRemove = new List<SmartCoin>();
			var toAdd = new List<SmartCoin>();

			// remove recursively the coins created by the transaction
			foreach (SmartCoin createdCoin in allCoins.CreatedBy(txId))
			{
				toRemove.AddRange(RemoveNoLock(createdCoin));
			}

			// destroyed (spent) coins are now (unspent)
			foreach (SmartCoin destroyedCoin in allCoins.SpentBy(txId))
			{
				if (SpentCoins.Remove(destroyedCoin))
				{
					destroyedCoin.SpenderTransaction = null;
					SpentCoinsByOutPoint.Remove(destroyedCoin.Outpoint);
					Coins.Add(destroyedCoin);
					toAdd.Add(destroyedCoin);
				}
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
				if (OutpointCoinCache.TryGetValue(input.PrevOut, out SmartCoin? coin))
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

	/// <summary>Gets transaction amount representing change in wallet balance for the wallet the transaction belongs to.</summary>
	/// <returns>The same value as <see cref="TransactionSummary.Amount"/>.</returns>
	public bool TryGetTxAmount(uint256 txid, [NotNullWhen(true)] out Money? amount)
	{
		lock (Lock)
		{
			return TransactionAmountsByTxid.TryGetValue(txid, out amount);
		}
	}

	/// <summary>Gets total balance as a sum of unspent coins.</summary>
	public Money GetTotalBalance()
	{
		lock (Lock)
		{
			// Amount can be hold as a variable that is updated every time to avoid summing it.
			return TransactionAmountsByTxid.Values.Sum();
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

	private ICoinsView DescendantOfAndSelfNoLock(SmartCoin coin) => AsAllCoinsViewNoLock().DescendantOfAndSelf(coin);

	public ICoinsView DescendantOfAndSelf(SmartCoin coin) => AsAllCoinsView().DescendantOfAndSelf(coin);

	public ICoinsView FilterBy(Func<SmartCoin, bool> expression) => AsCoinsView().FilterBy(expression);

	public IEnumerator<SmartCoin> GetEnumerator() => AsCoinsView().GetEnumerator();

	public ICoinsView CreatedBy(uint256 txid) => AsCoinsView().CreatedBy(txid);

	public ICoinsView SpentBy(uint256 txid) => AsSpentCoinsView().SpentBy(txid);

	public SmartCoin[] ToArray() => AsCoinsView().ToArray();

	public Money TotalAmount() => AsCoinsView().TotalAmount();

	public ICoinsView Unconfirmed() => AsCoinsView().Unconfirmed();

	public ICoinsView Unspent() => AsCoinsView().Unspent();

	IEnumerator IEnumerable.GetEnumerator() => AsCoinsView().GetEnumerator();
}
