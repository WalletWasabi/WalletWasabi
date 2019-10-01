using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Models
{
	public class CoinsRegistry
	{
		private ConcurrentHashSet<SmartCoin> Coins { get; }
		private object Lock { get; }
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public CoinsRegistry()
		{
			Coins = new ConcurrentHashSet<SmartCoin>();
			Lock = new object();
		}

		public CoinsView AsCoinsView()
		{
			return new CoinsView(Coins);
		}

		public bool IsEmpty => !Coins.Any();

		public SmartCoin GetByOutPoint(OutPoint outpoint)
		{
			return Coins.FirstOrDefault(x => x.GetOutPoint() == outpoint);
		}

		public bool TryAdd(SmartCoin coin)
		{
			var added = false;
			lock (Lock)
			{
				if (Coins.TryAdd(coin))
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
					if (Coins.TryRemove(toRemove))
					{
						removed.Add(coin);
					}
				}
			}
			if(removed.Count > 0)
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed));
			}
		}

		public void Clear()
		{
			var cleaned = false;
			lock (Lock)
			{
				if(Coins.Count > 0)
				{
					Coins.Clear();
					cleaned = true;
				}
			}
			if(cleaned)
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}
	}
}
