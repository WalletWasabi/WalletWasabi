using System;
using System.Collections;
using System.Collections.Generic;

namespace Nito.Collections
{
	internal static class CollectionHelpers
	{
		public static IReadOnlyCollection<T> ReifyCollection<T>(IEnumerable<T> source)
		{
			if (source is null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			if (source is IReadOnlyCollection<T> result)
			{
				return result;
			}

			if (source is ICollection<T> collection)
			{
				return new CollectionWrapper<T>(collection);
			}

			if (source is ICollection nongenericCollection)
			{
				return new NongenericCollectionWrapper<T>(nongenericCollection);
			}

			return new List<T>(source);
		}

		private sealed class NongenericCollectionWrapper<T> : IReadOnlyCollection<T>
		{
			private readonly ICollection _collection;

			public NongenericCollectionWrapper(ICollection collection)
			{
				_collection = collection ?? throw new ArgumentNullException(nameof(collection));
			}

			public int Count => _collection.Count;

			public IEnumerator<T> GetEnumerator()
			{
				foreach (T item in _collection)
				{
					yield return item;
				}
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return _collection.GetEnumerator();
			}
		}

		private sealed class CollectionWrapper<T> : IReadOnlyCollection<T>
		{
			private readonly ICollection<T> _collection;

			public CollectionWrapper(ICollection<T> collection)
			{
				_collection = collection ?? throw new ArgumentNullException(nameof(collection));
			}

			public int Count => _collection.Count;

			public IEnumerator<T> GetEnumerator()
			{
				return _collection.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return _collection.GetEnumerator();
			}
		}
	}
}
