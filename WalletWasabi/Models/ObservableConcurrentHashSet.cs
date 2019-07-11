using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace WalletWasabi.Models
{
	public class ObservableConcurrentHashSet<T> : IReadOnlyCollection<T>, INotifyCollectionChanged
	{
		private ConcurrentHashSet<T> Set { get; }
		private object Lock { get; }

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public ObservableConcurrentHashSet()
		{
			Set = new ConcurrentHashSet<T>();
			Lock = new object();
		}

		// Do not lock here, it results deadlock at wallet loading when filters are not synced.
		public int Count => Set.Count;

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		// Do not lock here, it results deadlock at wallet loading when filters are not synced.
		public IEnumerator<T> GetEnumerator() => Set.GetEnumerator();

		public bool TryAdd(T item)
		{
			var invoke = false;
			lock (Lock)
			{
				if (Set.TryAdd(item))
				{
					invoke = true;
				}
			}
			if (invoke)
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
			}
			return invoke;
		}

		public bool TryRemove(T item)
		{
			var invoke = false;
			lock (Lock)
			{
				if (Set.TryRemove(item))
				{
					invoke = true;
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
				if (Set.Count > 0)
				{
					Set.Clear();
					invoke = true;
				}
			}
			if (invoke)
			{
				// "Reset action must be initialized with no changed items."
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}

		// Do not lock here, it results deadlock at wallet loading when filters are not synced.
		public bool Contains(T item) => Set.Contains(item);
	}
}
