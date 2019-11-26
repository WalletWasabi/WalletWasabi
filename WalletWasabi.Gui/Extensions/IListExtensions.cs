using System;
using System.Collections.Generic;

namespace WalletWasabi.Gui.Extensions
{
	public static class IListExtensions
	{
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
			if (list == null)
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
		/// <typeparam name="T">is the type of items in the collection.</typeparam>
		/// <param name="myself">is "this" reference.</param>
		/// <param name="item">is the item to insert.</param>
		public static void InsertSorted<T>(this IList<T> myself, T item, bool exclusive = true) where T : IComparable<T>
		{
			var index = myself.BinarySearchIndexOf(item);

			if (index < 0)
			{
				myself.Insert(~index, item);
			}
			else if (!exclusive)
			{
				myself.Insert(index, item);
			}
		}

		/// <summary>
		///     Inserts an element into the collection, keeping it sorted. The collection must be sorted
		///     already, i.e. populated only with this method. The template type for the collection must
		///     implement IComparable.
		/// </summary>
		/// <typeparam name="T">is the type of items in the collection.</typeparam>
		/// <param name="myself">is "this" reference.</param>
		/// <param name="item">is the item to insert.</param>
		public static T InsertSortedExclusive<T>(this IList<T> myself, T item) where T : IComparable<T>
		{
			var index = myself.BinarySearchIndexOf(item);

			if (index < 0)
			{
				myself.Insert(~index, item);
			}
			else
			{
				return myself[index];
			}

			return default;
		}
	}
}
