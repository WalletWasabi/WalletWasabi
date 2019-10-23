using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Models
{
	public class CoinsRegistry : ICoinsView
	{
		private HashSet<SmartCoin> Coins { get; }
		private HashSet<SmartCoin> LatestCoinsSnapshot { get; set; }
		private bool InvalidateSnapshot { get; set; }
		private object Lock { get; set; }
		private HashSet<SmartCoin> SpentCoins { get; }
		private Dictionary<Script, Cluster> ClustersByScriptPubKey { get; }
		private int PrivacyLevelThreshold { get; }

		public CoinsRegistry(int privacyLevelThreshold)
		{
			Coins = new HashSet<SmartCoin>();
			SpentCoins = new HashSet<SmartCoin>();
			LatestCoinsSnapshot = new HashSet<SmartCoin>();
			InvalidateSnapshot = false;
			ClustersByScriptPubKey = new Dictionary<Script, Cluster>();
			PrivacyLevelThreshold = privacyLevelThreshold;
			Lock = new object();
		}

		private CoinsView AsCoinsViewNoLock()
		{
			if (InvalidateSnapshot)
			{
				LatestCoinsSnapshot = Coins.ToHashSet(); // Creates a clone
				InvalidateSnapshot = false;
			}
			return new CoinsView(LatestCoinsSnapshot);
		}

		private CoinsView AsCoinsView()
		{
			lock (Lock)
			{
				return AsCoinsViewNoLock();
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
						}
						InvalidateSnapshot = true;
					}
				}
			}
			return added;
		}

		public void Remove(SmartCoin coin)
		{
			lock (Lock)
			{
				var coinsToRemove = DescendantOfAndSelf(coin).ToList();
				foreach (var toRemove in coinsToRemove)
				{
					if (Coins.Remove(toRemove))
					{
						//Clusters.Remove(toRemove);
					}
				}
				InvalidateSnapshot = true;
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
					var createdCoins = CreatedBy(spentCoin.SpenderTransactionId);
					foreach (var newCoin in createdCoins)
					{
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

		public void RemoveFromBlock(Height blockHeight)
		{
			lock (Lock)
			{
				var allCoins = AsAllCoinsView();
				foreach (var toRemove in allCoins.AtBlockHeight(blockHeight).ToList())
				{
					var coinsToRemove = allCoins.DescendantOfAndSelf(toRemove).ToList();
					foreach (var coin in coinsToRemove)
					{
						if (coin.Unspent)
						{
							if (Coins.Remove(coin))
							{
								InvalidateSnapshot = true;
							}
						}
						else
						{
							SpentCoins.Remove(toRemove);
						}
					}
				}
			}
		}

		public ICoinsView AsAllCoinsView()
		{
			lock (Lock)
			{
				return new CoinsView(AsCoinsViewNoLock().Concat(SpentCoins).ToList());
			}
		}

		public ICoinsView AtBlockHeight(Height height)
		{
			return AsCoinsView().AtBlockHeight(height);
		}

		public ICoinsView Available()
		{
			return AsCoinsView().Available();
		}

		public ICoinsView ChildrenOf(SmartCoin coin)
		{
			return AsCoinsView().ChildrenOf(coin);
		}

		public ICoinsView CoinJoinInProcess()
		{
			return AsCoinsView().CoinJoinInProcess();
		}

		public ICoinsView Confirmed()
		{
			return AsCoinsView().Confirmed();
		}

		public ICoinsView DescendantOf(SmartCoin coin)
		{
			return AsCoinsView().DescendantOf(coin);
		}

		public ICoinsView DescendantOfAndSelf(SmartCoin coin)
		{
			return AsCoinsView().DescendantOfAndSelf(coin);
		}

		public ICoinsView FilterBy(Func<SmartCoin, bool> expression)
		{
			return AsCoinsView().FilterBy(expression);
		}

		public IEnumerator<SmartCoin> GetEnumerator()
		{
			return AsCoinsView().GetEnumerator();
		}

		public ICoinsView OutPoints(IEnumerable<TxoRef> outPoints)
		{
			return AsCoinsView().OutPoints(outPoints);
		}

		public ICoinsView CreatedBy(uint256 txid)
		{
			return AsCoinsView().CreatedBy(txid);
		}

		public ICoinsView SpentBy(uint256 txid)
		{
			return AsCoinsView().SpentBy(txid);
		}

		public SmartCoin[] ToArray()
		{
			return AsCoinsView().ToArray();
		}

		public Money TotalAmount()
		{
			return AsCoinsView().TotalAmount();
		}

		public ICoinsView Unconfirmed()
		{
			return AsCoinsView().Unconfirmed();
		}

		public ICoinsView UnSpent()
		{
			return AsCoinsView().UnSpent();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return AsCoinsView().GetEnumerator();
		}
	}
}
