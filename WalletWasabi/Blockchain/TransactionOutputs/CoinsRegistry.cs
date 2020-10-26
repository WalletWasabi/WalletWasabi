using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs
{
	public class CoinsRegistry : ICoinsView
	{
		public CoinsRegistry(int privacyLevelThreshold)
		{
			Coins = new HashSet<SmartCoin>();
			SpentCoins = new HashSet<SmartCoin>();
			LatestCoinsSnapshot = new HashSet<SmartCoin>();
			LatestSpentCoinsSnapshot = new HashSet<SmartCoin>();
			InvalidateSnapshot = false;
			ClustersByScriptPubKey = new Dictionary<Script, Cluster>();
			CoinsByOutPoint = new Dictionary<OutPoint, HashSet<SmartCoin>>();
			PrivacyLevelThreshold = privacyLevelThreshold;
			Lock = new object();
		}

		private HashSet<SmartCoin> Coins { get; }
		private HashSet<SmartCoin> LatestCoinsSnapshot { get; set; }
		private bool InvalidateSnapshot { get; set; }
		private object Lock { get; set; }
		private HashSet<SmartCoin> SpentCoins { get; }
		private HashSet<SmartCoin> LatestSpentCoinsSnapshot { get; set; }
		private Dictionary<Script, Cluster> ClustersByScriptPubKey { get; }
		private Dictionary<OutPoint, HashSet<SmartCoin>> CoinsByOutPoint { get; }
		private int PrivacyLevelThreshold { get; }

		public bool IsEmpty => !AsCoinsView().Any();

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

		public SmartCoin GetByOutPoint(OutPoint outpoint) => AsCoinsView().GetByOutPoint(outpoint);

		public bool TryAdd(SmartCoin coin)
		{
			var added = false;
			lock (Lock)
			{
				if (!SpentCoins.Contains(coin))
				{
					added = Coins.Add(coin);
					if (added)
					{
						if (ClustersByScriptPubKey.TryGetValue(coin.ScriptPubKey, out var cluster))
						{
							coin.Cluster = cluster;
						}
						else
						{
							ClustersByScriptPubKey.Add(coin.ScriptPubKey, coin.Cluster);
						}

						foreach (var spentOutPoint in coin.SpentOutputs)
						{
							var outPoint = spentOutPoint;
							var newCoinSet = new HashSet<SmartCoin> { coin };

							// If we don't succeed to add a new entry to the dictionary.
							if (!CoinsByOutPoint.TryAdd(outPoint, newCoinSet))
							{
								var previousCoinTxId = CoinsByOutPoint[outPoint].First().TransactionId;

								// Then check if we're in the same transaction as the previous coins in the dictionary are.
								if (coin.TransactionId == previousCoinTxId)
								{
									// If we are in the same transaction, then just add it to value set.
									CoinsByOutPoint[outPoint].Add(coin);
								}
								else
								{
									// If we aren't in the same transaction, then it's a conflict, so replace the old set with the new one.
									CoinsByOutPoint[outPoint] = newCoinSet;
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
					SpentCoins.Remove(toRemove);
				}

				var removedCoinOutPoint = toRemove.OutPoint;

				// If we can find it in our outpoint to coins cache.
				if (TryGetSpenderSmartCoinsByOutPointNoLock(removedCoinOutPoint, out var coinsByOutPoint))
				{
					// Go through all the coins of that cache where the coin is the coin we are wishing to remove.
					foreach (var coinByOutPoint in coinsByOutPoint.Where(x => x == toRemove))
					{
						// Remove the coin from the set, and if the set becomes empty as a consequence remove the key too.
						if (CoinsByOutPoint[removedCoinOutPoint].Remove(coinByOutPoint) && !CoinsByOutPoint[removedCoinOutPoint].Any())
						{
							CoinsByOutPoint.Remove(removedCoinOutPoint);
						}
					}
				}
			}
			InvalidateSnapshot = true;
			return coinsToRemove;
		}

		public void Spend(SmartCoin spentCoin)
		{
			lock (Lock)
			{
				if (Coins.Remove(spentCoin))
				{
					InvalidateSnapshot = true;
					SpentCoins.Add(spentCoin);
					var createdCoins = CreatedByNoLock(spentCoin.SpenderTransactionId);
					foreach (var newCoin in createdCoins)
					{
						if (newCoin.AnonymitySet < PrivacyLevelThreshold)
						{
							spentCoin.Cluster.Merge(newCoin.Cluster);
							newCoin.Cluster = spentCoin.Cluster;
							ClustersByScriptPubKey.AddOrReplace(newCoin.ScriptPubKey, newCoin.Cluster);
						}
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

		public bool TryGetSpenderSmartCoinsByOutPoint(OutPoint outPoint, out HashSet<SmartCoin> coins)
		{
			lock (Lock)
			{
				return TryGetSpenderSmartCoinsByOutPointNoLock(outPoint, out coins);
			}
		}

		private bool TryGetSpenderSmartCoinsByOutPointNoLock(OutPoint outPoint, out HashSet<SmartCoin> coins)
		{
			return CoinsByOutPoint.TryGetValue(outPoint, out coins);
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

		public ICoinsView OutPoints(IEnumerable<OutPoint> outPoints) => AsCoinsView().OutPoints(outPoints);

		public ICoinsView OutPoints(TxInList txIns) => AsCoinsView().OutPoints(txIns);

		public ICoinsView CreatedBy(uint256 txid) => AsCoinsView().CreatedBy(txid);

		private ICoinsView CreatedByNoLock(uint256 txid) => AsCoinsViewNoLock().CreatedBy(txid);

		public ICoinsView SpentBy(uint256 txid) => AsSpentCoinsView().SpentBy(txid);

		private ICoinsView SpentByNoLock(uint256 txid) => AsSpentCoinsViewNoLock().SpentBy(txid);

		public SmartCoin[] ToArray() => AsCoinsView().ToArray();

		public Money TotalAmount() => AsCoinsView().TotalAmount();

		public ICoinsView Unconfirmed() => AsCoinsView().Unconfirmed();

		public ICoinsView Unspent() => AsCoinsView().Unspent();

		IEnumerator IEnumerable.GetEnumerator() => AsCoinsView().GetEnumerator();
	}
}
