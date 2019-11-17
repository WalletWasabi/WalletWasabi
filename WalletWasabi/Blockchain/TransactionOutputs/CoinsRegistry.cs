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
		private int PrivacyLevelThreshold { get; }

		public CoinsRegistry(int privacyLevelThreshold)
		{
			Coins = new HashSet<SmartCoin>();
			SpentCoins = new HashSet<SmartCoin>();
			LatestCoinsSnapshot = new HashSet<SmartCoin>();
			LatestSpentCoinsSnapshot = new HashSet<SmartCoin>();
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
						if (SpentCoins.Remove(toRemove))
						{
							//Clusters.Remove(toRemove);
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
					var createdCoins = CreatedByNoLock(spentCoin.SpenderTransactionId);
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
				var allCoins = AsAllCoinsViewNoLock();
				var toRemove = new List<SmartCoin>();
				var toAdd = new List<SmartCoin>();

				// remove recursively the coins created by the transaction
				foreach (SmartCoin createdCoin in allCoins.CreatedBy(txId))
				{
					toRemove.AddRange(Remove(createdCoin));
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

		public ICoinsView OutPoints(IEnumerable<TxoRef> outPoints) => AsCoinsView().OutPoints(outPoints);

		public ICoinsView CreatedBy(uint256 txid) => AsCoinsView().CreatedBy(txid);

		private ICoinsView CreatedByNoLock(uint256 txid) => AsCoinsViewNoLock().CreatedBy(txid);

		public ICoinsView SpentBy(uint256 txid) => AsSpentCoinsView().SpentBy(txid);

		public SmartCoin[] ToArray() => AsCoinsView().ToArray();

		public Money TotalAmount() => AsCoinsView().TotalAmount();

		public ICoinsView Unconfirmed() => AsCoinsView().Unconfirmed();

		public ICoinsView Unspent() => AsCoinsView().Unspent();

		IEnumerator IEnumerable.GetEnumerator() => AsCoinsView().GetEnumerator();
	}
}
