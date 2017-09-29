using System.Collections.Generic;
using System.Collections.Specialized;
using ConcurrentCollections;

namespace System.Collections.ObjectModel
{
	public class ConcurrentObservableHashSet<T> : INotifyCollectionChanged, IReadOnlyCollection<T>
	{
		protected ConcurrentHashSet<T> ConcurrentHashSet { get; }

		private readonly object Lock = new object();

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
			lock(Lock)
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
			lock(Lock)
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
			lock(Lock)
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
