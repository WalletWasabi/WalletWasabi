using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;

namespace WalletWasabi.Extensions;

public static class LinqExtensions
{
	public static IEnumerable<IEnumerable<T>> Batch<T>(
	   this IEnumerable<T> source, int size)
	{
		T[]? bucket = null;
		var count = 0;

		foreach (var item in source)
		{
			bucket ??= new T[size];

			bucket[count++] = item;

			if (count != size)
			{
				continue;
			}

			yield return bucket.Select(x => x);

			bucket = null;
			count = 0;
		}

		// Return the last bucket with all remaining elements
		if (bucket is { } && count > 0)
		{
			Array.Resize(ref bucket, count);
			yield return bucket.Select(x => x);
		}
	}

	public static T? RandomElement<T>(this IEnumerable<T> source)
	{
		T? current = default;
		int count = 0;
		foreach (T element in source)
		{
			count++;
			if (Random.Shared.Next(count) == 0)
			{
				current = element;
			}
		}
		return current;
	}

	public static IList<T> Shuffle<T>(this IList<T> list)
	{
		int n = list.Count;
		while (n > 1)
		{
			n--;
			int k = Random.Shared.Next(n + 1);
			T value = list[k];
			list[k] = list[n];
			list[n] = value;
		}
		return list;
	}

	public static IList<T> ToShuffled<T>(this IEnumerable<T> list)
	{
		return list.ToList().Shuffle();
	}

	public static void AddToValueList<TKey, TElem>(this Dictionary<TKey, List<TElem>> myDic, TKey key, TElem elem) where TKey : notnull
	{
		if (myDic.ContainsKey(key))
		{
			myDic[key].Add(elem);
		}
		else
		{
			myDic.Add(key, new List<TElem>() { elem });
		}
	}

	public static bool NotNullAndNotEmpty<T>(this IEnumerable<T> source)
		=> source?.Any() is true;

	/// <summary>
	/// Recursive algorithm that generates all possible combinations of input <paramref name="items"/> with <paramref name="ofLength"/> length.
	/// </summary>
	/// <remarks>If you have numbers <c>1, 2, 3, 4</c>, then the output will contain <c>(2, 3, 4)</c> but not, for example, <c>(4, 3, 2)</c>.</remarks>
	public static IEnumerable<IEnumerable<T>> CombinationsWithoutRepetition<T>(
		this IEnumerable<T> items,
		int ofLength)
	{
		return (ofLength == 1)
			? items.Select(item => new[] { item })
			: items.SelectMany((item, i) => items
				.Skip(i + 1)
				.CombinationsWithoutRepetition(ofLength - 1)
				.Select(result => new T[] { item }.Concat(result)));
	}

	public static IEnumerable<IEnumerable<T>> CombinationsWithoutRepetition<T>(
		this IEnumerable<T> items,
		int ofLength,
		int upToLength)
	{
		return Enumerable
			.Range(ofLength, Math.Max(0, upToLength - ofLength + 1))
			.SelectMany(len => items.CombinationsWithoutRepetition(ofLength: len));
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
			.ThenBy(x => x.FirstSeen);

	public static IOrderedEnumerable<TransactionSummary> OrderByBlockchain(this IEnumerable<TransactionSummary> me)
		=> me
			.OrderBy(x => x.Height)
			.ThenBy(x => x.BlockIndex)
			.ThenBy(x => x.DateTime);

	public static IEnumerable<string> ToBlockchainOrderedLines(this IEnumerable<SmartTransaction> me)
		=> me
			.OrderByBlockchain()
			.Select(x => x.ToLine());

	/// <summary>
	/// Chunks the source list to sub-lists by the specified chunk size.
	/// Source: https://stackoverflow.com/a/24087164/2061103
	/// </summary>
	public static IEnumerable<IEnumerable<T>> ChunkBy<T>(this IEnumerable<T> source, int chunkSize)
	{
		return source
			.Select((x, i) => new { Index = i, Value = x })
			.GroupBy(x => x.Index / chunkSize)
			.Select(x => x.Select(v => v.Value));
	}

	/// <summary>
	/// Creates a tuple collection from two collections. If lengths differ, exception is thrown.
	/// </summary>
	public static IEnumerable<(T1, T2)> ZipForceEqualLength<T1, T2>(this IEnumerable<T1> source, IEnumerable<T2> otherCollection)
	{
		if (source.Count() != otherCollection.Count())
		{
			throw new InvalidOperationException($"{nameof(source)} and {nameof(otherCollection)} collections must have the same number of elements. {nameof(source)}:{source.Count()}, {nameof(otherCollection)}:{otherCollection.Count()}.");
		}
		return source.Zip(otherCollection);
	}

	public static IEnumerable<TAccumulate> Scan<TSource, TAccumulate>(
		this IEnumerable<TSource> source,
		TAccumulate seed,
		Func<TAccumulate, TSource, TAccumulate> func)
	{
		TAccumulate previous = seed;
		foreach (var item in source)
		{
			previous = func(previous, item);
			yield return previous;
		}
	}

	public static bool IsSuperSetOf<T>(this IEnumerable<T> me, IEnumerable<T> other) =>
		other.All(x => me.Contains(x));

	public static ulong Sum(this IEnumerable<ulong> me)
	{
		ulong inputSum = 0;
		foreach (var item in me)
		{
			inputSum += item;
		}
		return inputSum;
	}
}
