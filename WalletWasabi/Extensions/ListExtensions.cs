namespace System.Collections.Generic
{
	public static class ListExtensions
	{
		public static void RemoveFirst<T>(this List<T> me)
		{
			me.RemoveAt(0);
		}

		public static void RemoveLast<T>(this List<T> me)
		{
			me.RemoveAt(me.Count - 1);
		}

		// Following BinarySearch methods are based on source from: https://stackoverflow.com/questions/967047/how-to-perform-a-binary-search-on-ilistt
		public static T BinarySearch<T, TKey>(this IList<T> list, Func<T, TKey> keySelector, TKey key)
			where TKey : IComparable<TKey>
		{
			var min = 0;
			var max = list.Count;
			while (min < max)
			{
				var mid = min + ((max - min) / 2);
				var midItem = list[mid];
				var midKey = keySelector(midItem);
				var comp = midKey.CompareTo(key);
				if (comp < 0)
				{
					min = mid + 1;
				}
				else if (comp > 0)
				{
					max = mid - 1;
				}
				else
				{
					return midItem;
				}
			}

			if (min == max && min < list.Count &&
				keySelector(list[min]).CompareTo(key) == 0)
			{
				return list[min];
			}

			return default;
		}

		public static T BinarySearch<T, TKey>(this IList<T> list, TKey key)
			where T : IComparable<TKey>
		{
			var min = 0;
			var max = list.Count;
			while (min < max)
			{
				var mid = min + ((max - min) / 2);
				var midItem = list[mid];

				var comp = midItem.CompareTo(key);
				if (comp < 0)
				{
					min = mid + 1;
				}
				else if (comp > 0)
				{
					max = mid - 1;
				}
				else
				{
					return midItem;
				}
			}

			if (min == max && min < list.Count &&
				list[min].CompareTo(key) == 0)
			{
				return list[min];
			}

			return default;
		}

		public static int BinarySearchIndexOf<T, TKey>(this IList<T> list, TKey value) where T : IComparable<TKey>
		{
			if (list is null)
			{
				throw new ArgumentNullException("list");
			}

			int lower = 0;
			int upper = list.Count - 1;

			while (lower <= upper)
			{
				int middle = lower + ((upper - lower) / 2);
				int comparisonResult = list[middle].CompareTo(value);

				if (comparisonResult < 0)
				{
					lower = middle + 1;
				}
				else if (comparisonResult > 0)
				{
					upper = middle - 1;
				}
				else
				{
					return middle;
				}
			}

			return ~lower;
		}

		/// <summary>
		///     Inserts an element into the collection, keeping it sorted. The collection must be sorted
		///     already, i.e. populated only with this method. The template type for the collection must
		///     implement IComparable.
		/// </summary>
		/// <typeparam name="T">The type of items in the collection.</typeparam>
		/// <param name="myself">The "this" reference.</param>
		/// <param name="item">The item to insert.</param>
		/// <param name="exclusive">True if duplicate items are allowed, False if they are not allowed.</param>
		/// <returns>True if an item was inserted, and false if no item was inserted.</returns>
		public static bool InsertSorted<T>(this IList<T> myself, T item, bool exclusive = true) where T : IComparable<T>
		{
			var index = myself.BinarySearchIndexOf(item);

			if (index < 0)
			{
				myself.Insert(~index, item);
				return true;
			}
			else if (!exclusive)
			{
				myself.Insert(index, item);
				return true;
			}

			return false;
		}
	}
}
