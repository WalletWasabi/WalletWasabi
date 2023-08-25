using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class CoinsRegistry : ICoinsView
{
	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> Coins { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> LatestCoinsSnapshot { get; set; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private bool InvalidateSnapshot { get; set; }

	private object Lock { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> SpentCoins { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private HashSet<SmartCoin> LatestSpentCoinsSnapshot { get; set; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<Height, Dictionary<OutPoint, HashSet<SmartCoin>>> CoinsByOutPoint { get; } = new();

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private Dictionary<OutPoint, SmartCoin> SpentCoinsByOutPoint { get; } = new();

	private CoinsView AsCoinsViewNoLock()
	{
		if (InvalidateSnapshot)
		{
			LatestCoinsSnapshot = Coins.ToHashSet(); // Creates a clone
			LatestSpentCoinsSnapshot = SpentCoins.ToHashSet(); // Creates a clone
			InvalidateSnapshot = false;
		}

		return new CoinsView(LatestCoinsSnapshot);
	}

	private CoinsView AsSpentCoinsViewNoLock()
	{
		if (InvalidateSnapshot)
		{
			LatestCoinsSnapshot = Coins.ToHashSet(); // Creates a clone
			LatestSpentCoinsSnapshot = SpentCoins.ToHashSet(); // Creates a clone
			InvalidateSnapshot = false;
		}

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

	public bool TryGetByOutPoint(OutPoint outpoint, [NotNullWhen(true)] out SmartCoin? coin) => AsCoinsView().TryGetByOutPoint(outpoint, out coin);

	private static bool IsMature(Height height, Height currentBlockchainTipHeight) => height != Height.Mempool && height < currentBlockchainTipHeight - 101;
	
	private void RemoveMatureEntriesCoinsByOutPoint(Height currentBlockchainTipHeight)
	{
		var keysToRemove = CoinsByOutPoint.Keys.Where(key => IsMature(key, currentBlockchainTipHeight)).ToList();

		foreach (var key in keysToRemove)
		{
			CoinsByOutPoint.Remove(key);
		}
	}
	
	public bool TryAdd(SmartCoin coin, Height currentBlockchainTipHeight)
	{
		var added = false;
		lock (Lock)
		{
			RemoveMatureEntriesCoinsByOutPoint(currentBlockchainTipHeight);
			if (!SpentCoins.Contains(coin))
			{
				added = Coins.Add(coin);
				coin.RegisterToHdPubKey();
				if (added)
				{
					// Only add to the cache if coin is immature
					if (!IsMature(coin.Height, currentBlockchainTipHeight))
					{
						foreach (var outPoint in coin.Transaction.Transaction.Inputs.Select(x => x.PrevOut))
						{
							var newCoinSet = new HashSet<SmartCoin> { coin };
							
							if (!CoinsByOutPoint.ContainsKey(coin.Transaction.Height))
							{
								CoinsByOutPoint.Add(coin.Transaction.Height, new Dictionary<OutPoint, HashSet<SmartCoin>>());
							}
							var entry = CoinsByOutPoint[coin.Transaction.Height];
							
							// If we don't succeed to add a new entry to the dictionary.
							if (!entry.TryAdd(outPoint, newCoinSet))
							{
								var previousCoinTxId = entry[outPoint].First().TransactionId;

								// Then check if we're in the same transaction as the previous coins in the dictionary are.
								if (coin.TransactionId == previousCoinTxId)
								{
									// If we are in the same transaction, then just add it to value set.
									entry[outPoint].Add(coin);
								}
								else
								{
									// If we aren't in the same transaction, then it's a conflict, so replace the old set with the new one.
									entry[outPoint] = newCoinSet;
								}
							}
						}
					}
					InvalidateSnapshot = true;
				}
			}
		}

		return added;
	}

	public ICoinsView Remove(SmartCoin coin)
	{
		lock (Lock)
		{
			return RemoveNoLock(coin);
		}
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

			toRemove.UnregisterFromHdPubKey();

			var removedCoinOutPoint = toRemove.Outpoint;

			// If we can find it in our outpoint to coins cache.
			if (TryGetSpenderSmartCoinsByOutPointNoLock(removedCoinOutPoint, out var coinsByOutPoint, out var height))
			{
				// Go through all the coins of that cache where the coin is the coin we are wishing to remove.
				foreach (var coinByOutPoint in coinsByOutPoint.Where(x => x == toRemove))
				{
					// Remove the coin from the set, and if the set becomes empty as a consequence remove the key too.
					if (CoinsByOutPoint[height][removedCoinOutPoint].Remove(coinByOutPoint) && !CoinsByOutPoint[height][removedCoinOutPoint].Any())
					{
						CoinsByOutPoint[height].Remove(removedCoinOutPoint);
					}
				}
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
			}
		}
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

	public bool TryGetSpenderSmartCoinsByOutPoint(OutPoint outPoint, [NotNullWhen(true)] out HashSet<SmartCoin>? coins, out Height height, Height? currentBlockchainTipHeight = null)
	{
		lock (Lock)
		{
			if (currentBlockchainTipHeight is { } currentBlockchainTipHeightNonNullable)
			{
				RemoveMatureEntriesCoinsByOutPoint(currentBlockchainTipHeightNonNullable);
			}

			return TryGetSpenderSmartCoinsByOutPointNoLock(outPoint, out coins, out height);
		}
	}

	private bool TryGetSpenderSmartCoinsByOutPointNoLock(OutPoint outPoint, [NotNullWhen(true)] out HashSet<SmartCoin>? coins, out Height height)
	{
		foreach (var kvp in CoinsByOutPoint)
		{
			if (kvp.Value.TryGetValue(outPoint, out coins))
			{
				height = kvp.Key;
				return true;
			}
		}
    
		coins = null;
		height = Height.Unknown;
		return false;
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
					SpentCoinsByOutPoint.Remove(destroyedCoin.Outpoint);
					Coins.Add(destroyedCoin);
					toAdd.Add(destroyedCoin);
				}
			}

			InvalidateSnapshot = true;

			return (new CoinsView(toRemove), new CoinsView(toAdd));
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
