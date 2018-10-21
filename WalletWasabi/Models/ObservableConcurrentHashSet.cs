using ConcurrentCollections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace WalletWasabi.Models
{
	public class ObservableConcurrentHashSet<T> : IReadOnlyCollection<T> // DO NOT IMPLEMENT INotifyCollectionChanged!!! That'll break and crash the software: https://github.com/AvaloniaUI/Avalonia/issues/1988#issuecomment-431691863
	{
		private ConcurrentHashSet<T> Set { get; }
		private object Lock { get; }

		public event EventHandler HashSetChanged; // Keep it as is! Unless with the modification this bug won't come out: https://github.com/AvaloniaUI/Avalonia/issues/1988#issuecomment-431691863

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
				if (Set.Add(item))
				{
					HashSetChanged?.Invoke(this, EventArgs.Empty);
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
				}
			}
		}

		// Don't lock here, it results deadlock at wallet loading when filters arent synced.
		public bool Contains(T item) => Set.Contains(item);
	}
}
