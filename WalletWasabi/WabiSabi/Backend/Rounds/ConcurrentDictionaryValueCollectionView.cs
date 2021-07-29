using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	// Expose the values of a concurrent dictionary as a mutable collection
	// using a function that maps to keys.
	internal class ConcurrentDictionaryValueCollectionView<T> : IEnumerable<T>
	{
		public ConcurrentDictionary<uint256, T> ConcurrentDictionary { get; init; }
		public Func<T, uint256> KeyFunc { get; init; }

		public int Count => ConcurrentDictionary.Count;

		public void Add(T value)
		{
			if (!ConcurrentDictionary.TryAdd(KeyFunc(value), value))
			{
				throw new ArgumentException("Duplicate key");
			}
		}

		public void Remove(T value)
		{
			ConcurrentDictionary.Remove(KeyFunc(value), out _);
		}

		public IEnumerator<T> GetEnumerator() => ConcurrentDictionary.Select(x => x.Value).GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
