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

			if (source is ICollection nonGenericCollection)
			{
				return new NonGenericCollectionWrapper<T>(nonGenericCollection);
			}

			return new List<T>(source);
		}

		private sealed class NonGenericCollectionWrapper<T> : IReadOnlyCollection<T>
		{
			private readonly ICollection Collection;

			public NonGenericCollectionWrapper(ICollection collection)
			{
				Collection = collection ?? throw new ArgumentNullException(nameof(collection));
			}

			public int Count => Collection.Count;

			public IEnumerator<T> GetEnumerator()
			{
				foreach (T item in Collection)
				{
					yield return item;
				}
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return Collection.GetEnumerator();
			}
		}

		private sealed class CollectionWrapper<T> : IReadOnlyCollection<T>
		{
			private readonly ICollection<T> Collection;

			public CollectionWrapper(ICollection<T> collection)
			{
				Collection = collection ?? throw new ArgumentNullException(nameof(collection));
			}

			public int Count => Collection.Count;

			public IEnumerator<T> GetEnumerator()
			{
				return Collection.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return Collection.GetEnumerator();
			}
		}
	}
}
