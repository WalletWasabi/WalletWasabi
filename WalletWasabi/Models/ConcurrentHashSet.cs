using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models
{
	[DebuggerDisplay("Count = {Count}")]
	public class ConcurrentHashSet<T> : IReadOnlyCollection<T>, IEnumerable<T>, IEnumerable
	{
		private const int DefaultConcurrencyLevel = 4;

		private const int DefaultCapacity = 4_000;

		private readonly ConcurrentDictionary<T, byte> _dictionary = new ConcurrentDictionary<T, byte>(DefaultConcurrencyLevel, DefaultCapacity);

		public int Count
			=> _dictionary.Count;

		public void Clear()
			=> _dictionary.Clear();

		public bool Contains(T item)
			=> _dictionary.ContainsKey(item);

		public bool TryAdd(T item)
			=> _dictionary.TryAdd(item, 0);

		public bool TryRemove(T item)
			=> _dictionary.TryRemove(item, out byte dontCare);

		public KeyEnumerator GetEnumerator()
			=> new KeyEnumerator(_dictionary);

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
			=> GetEnumeratorImpl();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumeratorImpl();

		private IEnumerator<T> GetEnumeratorImpl()
		{
			foreach (var kvp in _dictionary)
			{
				yield return kvp.Key;
			}
		}


		public struct KeyEnumerator : IEnumerator<T>
		{
			private readonly IEnumerator<KeyValuePair<T, byte>> _kvpEnumerator;

			internal KeyEnumerator(IEnumerable<KeyValuePair<T, byte>> data)
			{
				_kvpEnumerator = data.GetEnumerator();
			}

			public T Current => _kvpEnumerator.Current.Key;

			object IEnumerator.Current => _kvpEnumerator.Current.Key;

			public void Dispose()
			{
			}

			public bool MoveNext()
			{
				return _kvpEnumerator.MoveNext();
			}

			public void Reset()
			{
				_kvpEnumerator.Reset();
			}
		}
	}
}
