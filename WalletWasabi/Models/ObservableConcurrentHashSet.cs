using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace WalletWasabi.Models
{
	public class ObservableConcurrentHashSet<T> : IReadOnlyCollection<T>, INotifyCollectionChanged
	{
		private ConcurrentHashSet<T> Set { get; }
		private object Lock { get; }

		public event EventHandler HashSetChanged;

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public ObservableConcurrentHashSet()
		{
			Set = new ConcurrentHashSet<T>();
			Lock = new object();
		}

		// Don't lock here, it results deadlock at wallet loading when filters arent synced.
		public int Count => Set.Count;

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		// Don't lock here, it results deadlock at wallet loading when filters arent synced.
		public IEnumerator<T> GetEnumerator() => Set.GetEnumerator();

		public bool TryAdd(T item)
		{
			var invoke=false;
			lock (Lock)
			{
				if (Set.TryAdd(item))
				{
					invoke = true;
				}
			}
			if (invoke)
			{
				HashSetChanged?.Invoke(this, EventArgs.Empty);
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new[] { item }));
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
				HashSetChanged?.Invoke(this, EventArgs.Empty);
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new[] { item }));
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
				HashSetChanged?.Invoke(this, EventArgs.Empty);
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}

		// Don't lock here, it results deadlock at wallet loading when filters arent synced.
		public bool Contains(T item) => Set.Contains(item);
	}
}
