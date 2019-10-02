using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Models
{
	public class CoinsRegistry
	{
		private HashSet<SmartCoin> Coins { get; }
		private object Lock { get; }

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public CoinsRegistry()
		{
			Coins = new HashSet<SmartCoin>();
			Lock = new object();
		}

		public CoinsView AsCoinsView()
		{
			lock (Lock)
			{
				return new CoinsView(Coins.ToList());
			}
		}

		public bool IsEmpty()
		{
			lock (Lock)
			{
				return !Coins.Any();
			}
		}

		public SmartCoin GetByOutPoint(OutPoint outpoint)
		{
			lock (Lock)
			{
				return Coins.FirstOrDefault(x => x.GetOutPoint() == outpoint);
			}
		}

		public bool TryAdd(SmartCoin coin)
		{
			var added = false;
			lock (Lock)
			{
				if (Coins.Add(coin))
				{
					added = true;
				}
			}

			if (added)
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, coin));
			}
			return added;
		}

		public void Remove(SmartCoin coin)
		{
			var coinsToRemove = AsCoinsView().DescendantOf(coin).ToList();
			coinsToRemove.Add(coin);
			var removed = new List<SmartCoin>();
			lock (Lock)
			{
				foreach (var toRemove in coinsToRemove)
				{
					if (Coins.Remove(toRemove))
					{
						removed.Add(coin);
					}
				}
			}
			if (removed.Count > 0)
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed));
			}
		}

		public void Clear()
		{
			var cleaned = false;
			lock (Lock)
			{
				if (Coins.Count > 0)
				{
					Coins.Clear();
					cleaned = true;
				}
			}
			if (cleaned)
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}
	}
}
