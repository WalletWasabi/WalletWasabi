using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace WalletWasabi.Models.Graphs
{
	public class ObservableConcurrentCoinGraph : IReadOnlyCollection<SmartCoin>, INotifyCollectionChanged
	{
		private ConcurrentHashSet<SmartCoin> Verticles { get; }
		private object Lock { get; }

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public ObservableConcurrentCoinGraph()
		{
			Verticles = new ConcurrentHashSet<SmartCoin>();
			Lock = new object();
		}

		// Don't lock here, it results deadlock at wallet loading when filters arent synced.
		public int Count => Verticles.Count;

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		// Don't lock here, it results deadlock at wallet loading when filters arent synced.
		public IEnumerator<SmartCoin> GetEnumerator() => Verticles.GetEnumerator();

		public bool TryAdd(SmartCoin item)
		{
			var invoke = false;
			lock (Lock)
			{
				if (Verticles.TryAdd(item))
				{
					invoke = true;

					foreach (var verticle in Verticles)
					{
						if (verticle != item && verticle.ScriptPubKey == item.ScriptPubKey) // Same address. ToDo: Should we rather check pubkey hashes here somehow?
						{
							CoinEdge.CreateOrUpdate(verticle, item, 1);
						}
					}
				}
			}
			if (invoke)
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
			}
			return invoke;
		}

		public bool TryRemove(SmartCoin item)
		{
			var invoke = false;
			lock (Lock)
			{
				if (Verticles.TryRemove(item))
				{
					invoke = true;

					// Remove all the edges related to this verticle.
					foreach (var edge in item.Edges)
					{
						CoinEdge.Remove(edge);
					}
				}
			}
			if (invoke)
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
			}
			return invoke;
		}

		public void Clear()
		{
			var invoke = false;
			lock (Lock)
			{
				if (Verticles.Count > 0)
				{
					Verticles.Clear();
					invoke = true;
				}
			}
			if (invoke)
			{
				// "Reset action must be initialized with no changed items."
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}

		// Don't lock here, it results deadlock at wallet loading when filters arent synced.
		public bool Contains(SmartCoin item) => Verticles.Contains(item);
	}
}
