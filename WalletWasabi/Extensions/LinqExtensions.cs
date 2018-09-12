using System.Collections.Generic;

namespace System.Linq
{
	public static class LinqExtensions
	{
		public static T RandomElement<T>(this IEnumerable<T> source)
		{
			T current = default;
			int count = 0;
			foreach (T element in source)
			{
				count++;
				if (new Random().Next(count) == 0)
				{
					current = element;
				}
			}
			if (count == 0)
			{
				return default;
			}
			return current;
		}

		// https://stackoverflow.com/a/2992364
		public static void RemoveByValue<TKey, TValue>(this Dictionary<TKey, TValue> me, TValue value)
		{
			var itemsToRemove = new List<TKey>();

			foreach (var pair in me)
			{
				if (pair.Value.Equals(value))
					itemsToRemove.Add(pair.Key);
			}

			foreach (TKey item in itemsToRemove)
			{
				me.Remove(item);
			}
		}

		// https://stackoverflow.com/a/2992364
		public static void RemoveByValue<TKey, TValue>(this SortedDictionary<TKey, TValue> me, TValue value)
		{
			var itemsToRemove = new List<TKey>();

			foreach (var pair in me)
			{
				if (pair.Value.Equals(value))
					itemsToRemove.Add(pair.Key);
			}

			foreach (TKey item in itemsToRemove)
			{
				me.Remove(item);
			}
		}

		public static bool NotNullAndNotEmpty<T>(this IEnumerable<T> source)
		{
			return !(source is null) && source.Any();
		}
	}
}
