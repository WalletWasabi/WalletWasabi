using System.Collections.Generic;
using System.Collections.Specialized;
using ConcurrentCollections;
using Nito.AsyncEx;

namespace System.Collections.ObjectModel
{
	public class ConcurrentObservableHashSet<T> : INotifyCollectionChanged, IReadOnlyCollection<T>
	{
		protected ConcurrentHashSet<T> ConcurrentHashSet { get; }

        private readonly AsyncLock _asyncLock = new AsyncLock();

		public ConcurrentObservableHashSet()
		{
			ConcurrentHashSet = new ConcurrentHashSet<T>();
		}

		public event NotifyCollectionChangedEventHandler CollectionChanged;
		private void OnCollectionChanged()
		{
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<T> GetEnumerator() => ConcurrentHashSet.GetEnumerator();
		
		public bool TryAdd(T item)
		{
			using(_asyncLock.Lock())
			{
				if(ConcurrentHashSet.Add(item))
				{
					OnCollectionChanged();
					return true;
				}
				return false;
			}
		}

		public void Clear()
		{
            using (_asyncLock.Lock())
            {
				if(ConcurrentHashSet.Count > 0)
				{
					ConcurrentHashSet.Clear();
					OnCollectionChanged();
				}
			}
		}

		public bool Contains(T item) => ConcurrentHashSet.Contains(item);

		public bool TryRemove(T item)
		{
            using (_asyncLock.Lock())
            {
				if(ConcurrentHashSet.TryRemove(item))
				{
					OnCollectionChanged();
					return true;
				}
				return false;
			}
		}

		public int Count => ConcurrentHashSet.Count;
	}
}
