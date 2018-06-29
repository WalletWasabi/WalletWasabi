using ConcurrentCollections;
using System.Collections.Generic;

namespace System.Collections.ObjectModel
{
	public class NotifyingConcurrentHashSet<T> : IReadOnlyCollection<T>
	{
		protected ConcurrentHashSet<T> ConcurrentHashSet { get; }

		public event EventHandler HashSetChanged;

		private object Lock { get; }

		public NotifyingConcurrentHashSet()
		{
			ConcurrentHashSet = new ConcurrentHashSet<T>();
			Lock = new object();
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<T> GetEnumerator() => ConcurrentHashSet.GetEnumerator();

		public bool TryAdd(T item)
		{
			lock (Lock)
			{
				if (ConcurrentHashSet.Add(item))
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
				if (ConcurrentHashSet.Count > 0)
				{
					ConcurrentHashSet.Clear();
					HashSetChanged?.Invoke(this, EventArgs.Empty);
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
					HashSetChanged?.Invoke(this, EventArgs.Empty);
					return true;
				}
				return false;
			}
		}

		public int Count => ConcurrentHashSet.Count;
	}
}
