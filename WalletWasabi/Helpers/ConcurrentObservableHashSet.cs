using System.Collections.Generic;
using System.Collections.Specialized;
using ConcurrentCollections;

namespace System.Collections.ObjectModel
{
	public class ConcurrentObservableHashSet<T> : INotifyCollectionChanged, IReadOnlyCollection<T>
	{
		protected ConcurrentHashSet<T> ConcurrentHashSet { get; }

		private object Lock { get; }

		public ConcurrentObservableHashSet()
		{
			ConcurrentHashSet = new ConcurrentHashSet<T>();
			Lock = new object();
		}

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<T> GetEnumerator() => ConcurrentHashSet.GetEnumerator();

		public bool TryAdd(T item)
		{
			lock (Lock)
			{
				if (ConcurrentHashSet.Add(item))
				{
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, Count - 1));
					return true;
				}
				return false;
			}
		}

		public void Clear()
		{
			lock (Lock)
			{
				if (ConcurrentHashSet.Count > 0)
				{
					ConcurrentHashSet.Clear();
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
			}
		}

		public bool Contains(T item) => ConcurrentHashSet.Contains(item);

		public bool TryRemove(T item)
		{
			lock (Lock)
			{
				if (ConcurrentHashSet.TryRemove(item))
				{
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, ConcurrentHashSet));
					return true;
				}
				return false;
			}
		}

		public int Count => ConcurrentHashSet.Count;
	}
}
