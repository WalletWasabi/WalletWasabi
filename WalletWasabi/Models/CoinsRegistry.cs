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

		public CoinsRegistry()
		{
			Coins = new HashSet<SmartCoin>();
			LatestCoinsSnapshot = new HashSet<SmartCoin>();
			InvalidateSnapshot = false;
			Lock = new object();
		}

		private CoinsView AsCoinsView()
		{
			lock (Lock)
			{
				if (InvalidateSnapshot)
				{
					LatestCoinsSnapshot = Coins.ToHashSet(); // Creates a clone
					InvalidateSnapshot = false;
				}
			}
			return new CoinsView(LatestCoinsSnapshot);
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
				added = Coins.Add(coin);
				InvalidateSnapshot |= added;
			}
			return added;
		}

		public void Remove(SmartCoin coin)
		{
			var coinsToRemove = AsCoinsView().DescendantOf(coin).ToList();
			coinsToRemove.Add(coin);
			var removedCoins = new List<SmartCoin>();
			lock (Lock)
			{
				foreach (var toRemove in coinsToRemove)
				{
					if (Coins.Remove(toRemove))
					{
						removedCoins.Add(toRemove);
					}
				}
				InvalidateSnapshot = true;
			}
		}

		public void Clear()
		{
			lock (Lock)
			{
				Coins.Clear();
				InvalidateSnapshot = true;
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
