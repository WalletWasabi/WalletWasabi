using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Models;

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

		public static void Shuffle<T>(this IList<T> list)
		{
			var rng = new Random();
			int n = list.Count;
			while (n > 1)
			{
				n--;
				int k = rng.Next(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}

		// https://stackoverflow.com/a/2992364
		public static void RemoveByValue<TKey, TValue>(this Dictionary<TKey, TValue> me, TValue value)
		{
			var itemsToRemove = new List<TKey>();

			foreach (var pair in me)
			{
				if (pair.Value.Equals(value))
				{
					itemsToRemove.Add(pair.Key);
				}
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
				{
					itemsToRemove.Add(pair.Key);
				}
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

		public static IEnumerable<IEnumerable<T>> GetPermutations<T>(this IEnumerable<T> items, int count)
		{
			int i = 0;
			foreach (var item in items)
			{
				if (count == 1)
				{
					yield return new T[] { item };
				}
				else
				{
					foreach (var result in items.Skip(i + 1).GetPermutations(count - 1))
					{
						yield return new T[] { item }.Concat(result);
					}
				}

				++i;
			}
		}

		public static IEnumerable<IEnumerable<SmartCoin>> GetPermutations(this IEnumerable<SmartCoin> items, int count, Money minAmount)
		{
			int i = 0;
			foreach (var item in items)
			{
				if (count == 1)
				{
					if (item.Amount >= minAmount)
					{
						yield return new SmartCoin[] { item };
					}
				}
				else
				{
					foreach (var result in items.Skip(i + 1).GetPermutations(count - 1))
					{
						if (item.Amount + result.Sum(x => x.Amount) >= minAmount)
						{
							yield return new SmartCoin[] { item }.Concat(result);
						}
					}
				}

				++i;
			}
		}

		public static IOrderedEnumerable<SmartTransaction> OrderByBlockchain(this IEnumerable<SmartTransaction> me)
			=> me
				.OrderBy(x => x.Height)
				.ThenBy(x => x.BlockIndex)
				.ThenBy(x => x.FirstSeenIfMempoolTime ?? DateTime.UtcNow);
	}
}
