using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Models
{
	public class CoinsRegistry
	{
		private HashSet<SmartCoin> _coins;
		private object _lock;
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public CoinsRegistry()
		{
			_coins = new HashSet<SmartCoin>();
			_lock = new object();
		}

		public CoinsView AsCoinsView()
		{
			return new CoinsView(_coins);
		}

		public bool IsEmpty => !_coins.Any();

		public SmartCoin GetByOutPoint(OutPoint outpoint)
		{
			return _coins.FirstOrDefault(x => x.GetOutPoint() == outpoint);
		}

		public bool TryAdd(SmartCoin coin)
		{
			var added = false;
			lock (_lock)
			{
				added = _coins.Add(coin);
			}

			if( added )
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, coin));
			}
			return added;
		}

		public void Remove(SmartCoin coin)
		{
			var coinsToRemove = AsCoinsView().DescendatOf(coin).ToList();
			coinsToRemove.Add(coin);
			lock (_lock)
			{
				foreach(var toRemove in coinsToRemove)
				{
					_coins.Remove(coin);
				}
			}
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, coinsToRemove));
		}

		public void Clear()
		{
			lock (_lock)
			{
				_coins.Clear();
			}
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
	}
}