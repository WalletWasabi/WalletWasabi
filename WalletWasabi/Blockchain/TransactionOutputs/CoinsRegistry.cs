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
		private HashSet<SmartCoin> Coins { get; }
		private HashSet<SmartCoin> LatestCoinsSnapshot { get; set; }
		private bool InvalidateSnapshot { get; set; }
		private object Lock { get; set; }
		private HashSet<SmartCoin> SpentCoins { get; }
		private HashSet<SmartCoin> LatestSpentCoinsSnapshot { get; set; }
		private Dictionary<Script, Cluster> ClustersByScriptPubKey { get; }
		private Dictionary<uint256, List<SmartCoin>> CoinsCreatedByTransaction { get; }
		private Dictionary<uint256, List<SmartCoin>> CoinsDestroyedByTransaction { get; }
		private Dictionary<OutPoint, SmartCoin> CoinsByOutPoint { get; }

		private int PrivacyLevelThreshold { get; }

		public CoinsRegistry(int privacyLevelThreshold)
		{
			Coins = new HashSet<SmartCoin>();
			SpentCoins = new HashSet<SmartCoin>();
			LatestCoinsSnapshot = new HashSet<SmartCoin>();
			LatestSpentCoinsSnapshot = new HashSet<SmartCoin>();
			InvalidateSnapshot = false;
			ClustersByScriptPubKey = new Dictionary<Script, Cluster>();
			CoinsCreatedByTransaction = new Dictionary<uint256, List<SmartCoin>>();
			CoinsDestroyedByTransaction = new Dictionary<uint256, List<SmartCoin>>();
			CoinsByOutPoint = new Dictionary<OutPoint, SmartCoin>();
			PrivacyLevelThreshold = privacyLevelThreshold;
			Lock = new object();
		}

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

		public bool IsEmpty => !AsCoinsView().Any();

		public SmartCoin GetByOutPoint(OutPoint outpoint)
		{
			return AsCoinsView().GetByOutPoint(outpoint);
		}

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
							coin.Clusters = cluster;
							foreach(var coinInCluster in cluster.Coins)
							{
								coin.ShareSameScriptPubKeyWith(coinInCluster);
								coinInCluster.ShareSameScriptPubKeyWith(coin);
							}
						}
						else
						{
							ClustersByScriptPubKey.Add(coin.ScriptPubKey, coin.Clusters);
						}

						if (CoinsCreatedByTransaction.TryGetValue(coin.TransactionId, out var coinList))
						{
							coinList.Add(coin);
						}
						else
						{
							CoinsCreatedByTransaction[coin.TransactionId] = new List<SmartCoin>{ coin };
						}

						foreach (var spentOutPoint in coin.SpentOutputs)
						{
							CoinsByOutPoint.AddOrReplace(spentOutPoint.ToOutPoint(), coin);
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
				var coinsToRemove = DescendantOfAndSelfNoLock(coin);
				foreach (var toRemove in coinsToRemove)
				{
					if (!Coins.Remove(toRemove))
					{
						SpentCoins.Remove(toRemove);
//						CoinsByOutPoint.Remove(toRemove.GetOutPoint());

						CoinsCreatedByTransaction[toRemove.TransactionId].Remove(toRemove);
						if (toRemove.SpenderTransactionId is { })
						{
							CoinsDestroyedByTransaction[toRemove.SpenderTransactionId].Remove(toRemove);
						}
						foreach(var link in toRemove.Links)
						{
							link.TargetCoin.Links.RemoveAll(l=>l.TargetCoin == coin);
						}
					}
				}
				InvalidateSnapshot = true;
				return coinsToRemove;
			}
		}

		public void Spend(SmartCoin spentCoin)
		{
			lock (Lock)
			{
				if (Coins.Remove(spentCoin))
				{
					InvalidateSnapshot = true;
					SpentCoins.Add(spentCoin);

					if (CoinsDestroyedByTransaction.TryGetValue(spentCoin.SpenderTransactionId, out var coinList))
					{
						coinList.Add(spentCoin);
					}
					else
					{
						CoinsDestroyedByTransaction[spentCoin.SpenderTransactionId] = new List<SmartCoin>{ spentCoin };
					}

					var createdCoins = CreatedByNoLock(spentCoin.SpenderTransactionId);
					foreach (var newCoin in createdCoins)
					{
						newCoin.Spends(spentCoin);
						spentCoin.SpentBy(newCoin);

						if (newCoin.AnonymitySet < PrivacyLevelThreshold)
						{
							spentCoin.Clusters.Merge(newCoin.Clusters);
							newCoin.Clusters = spentCoin.Clusters;
							ClustersByScriptPubKey.AddOrReplace(newCoin.ScriptPubKey, newCoin.Clusters);
						}
					}
				}
			}
		}

		public void SwitchToUnconfirmFromBlock(Height blockHeight)
		{
			lock (Lock)
			{
				foreach (var coin in AsCoinsView().AtBlockHeight(blockHeight))
				{
					var descendantCoins = DescendantOfAndSelf(coin);
					foreach (var toSwitch in descendantCoins)
					{
						toSwitch.Height = Height.Mempool;
					}
				}
			}
		}

		internal (ICoinsView toRemove, ICoinsView toAdd) Undo(uint256 txId)
		{
			lock (Lock)
			{
				var toRemove = new List<SmartCoin>();
				var toAdd = new List<SmartCoin>();

				// remove recursively the coins created by the transaction
				foreach (SmartCoin createdCoin in CreatedByNoLock(txId).ToList())
				{
					toRemove.AddRange(Remove(createdCoin));
				}
				// destroyed (spent) coins are now (unspent)
				foreach (SmartCoin destroyedCoin in SpentByNoLock(txId).ToList())
				{
					if (SpentCoins.Remove(destroyedCoin))
					{
						Coins.Add(destroyedCoin);
						toAdd.Add(destroyedCoin);
					}
				}

				CoinsCreatedByTransaction.Remove(txId);
				CoinsDestroyedByTransaction.Remove(txId);

				InvalidateSnapshot = true;
				return (new CoinsView(toRemove), new CoinsView(toAdd));
			}
		}

		public bool TryGetSpentSmartCoinsByOutput(OutPoint outPoint, out SmartCoin coin)
		{
			lock(Lock)
			{
				if (CoinsByOutPoint.ContainsKey(outPoint))
				{
					coin = CoinsByOutPoint[outPoint];
					return true;
				}
				coin = null;
				return false;
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

		public ICoinsView OutPoints(IEnumerable<TxoRef> outPoints) => AsCoinsView().OutPoints(outPoints);

		private static List<SmartCoin> EmptyListOfSmartCoins = new List<SmartCoin>(0);
		public ICoinsView CreatedBy(uint256 txid)
		{
			lock(Lock)
			{
				return CreatedByNoLock(txid);
			}
		}

		public ICoinsView SpentBy(uint256 txid)
		{
			lock(Lock)
			{
				return SpentByNoLock(txid);
			}
		}

		private ICoinsView CreatedByNoLock(uint256 txid)
		{
			if (!CoinsCreatedByTransaction.TryGetValue(txid, out var coins))
			{
				coins = EmptyListOfSmartCoins;
			}
			return new CoinsView(coins);
		}

		private ICoinsView SpentByNoLock(uint256 txid)
		{
			if (!CoinsDestroyedByTransaction.TryGetValue(txid, out var coins))
			{
				coins = EmptyListOfSmartCoins;
			}
			return new CoinsView(coins);
		}
		
		public SmartCoin[] ToArray() => AsCoinsView().ToArray();

		public Money TotalAmount() => AsCoinsView().TotalAmount();

		public ICoinsView Unconfirmed() => AsCoinsView().Unconfirmed();

		public ICoinsView Unspent() => AsCoinsView().Unspent();

		IEnumerator IEnumerable.GetEnumerator() => AsCoinsView().GetEnumerator();
	}
}
