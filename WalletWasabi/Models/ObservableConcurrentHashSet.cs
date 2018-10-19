using ConcurrentCollections;
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

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public ObservableConcurrentHashSet()
		{
			Set = new ConcurrentHashSet<T>();
			Lock = new object();
		}

		public int Count
		{
			get
			{
				lock (Lock)
				{
					return Set.Count;
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<T> GetEnumerator()
		{
			lock (Lock)
			{
				return Set.GetEnumerator();
			}
		}

		public bool TryAdd(T item)
		{
			lock (Lock)
			{
				if (Set.Add(item))
				{
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
					return true;
				}
				return false;
			}
		}

		public bool TryRemove(T item)
		{
			lock (Lock)
			{
				if (Set.TryRemove(item))
				{
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
					return true;
				}
				return false;
			}
		}

		public void Clear()
		{
			lock (Lock)
			{
				if (Set.Count > 0)
				{
					Set.Clear();
					// "Reset action must be initialized with no changed items."
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
			}
		}

		public bool Contains(T item)
		{
			lock (Lock)
			{
				return Set.Contains(item);
			}
		}
	}
}
