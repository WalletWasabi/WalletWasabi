using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace WalletWasabi.Models
{
	public class ObservableConcurrentHashSet<T> : IReadOnlyCollection<T>, INotifyCollectionChanged, IObservable<T>
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
			lock (Lock)
			{
				if (Set.TryAdd(item))
				{
					HashSetChanged?.Invoke(this, EventArgs.Empty);
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new[] { item }));
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
					HashSetChanged?.Invoke(this, EventArgs.Empty);
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new[] { item }));
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
					HashSetChanged?.Invoke(this, EventArgs.Empty);
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
			}
		}

		// Don't lock here, it results deadlock at wallet loading when filters arent synced.
		public bool Contains(T item) => Set.Contains(item);

		public IDisposable Subscribe(IObserver<T> observer)
		{
			throw new NotImplementedException();
		}
	}
}
