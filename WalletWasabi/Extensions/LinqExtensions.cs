using System.Collections.Generic;
using System.Linq;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Extensions;

public static class LinqExtensions
{
	public static T? RandomElement<T>(this IEnumerable<T> source, WasabiRandom random)
	{
		T? current = default;
		int count = 0;
		foreach (T element in source)
		{
			count++;

			if (random.GetInt(0, count) == 0)
			{
				current = element;
			}
		}
		return current;
	}

	/// <summary>
	/// Selects a random element based on order bias.
	/// </summary>
	/// <param name="biasPercent">1-100, eg. if 80, then 80% probability for the first element.</param>
	public static T? BiasedRandomElement<T>(this IEnumerable<T> source, int biasPercent, WasabiRandom random)
	{
		foreach (T element in source)
		{
			if (random.GetInt(1, 101) <= biasPercent)
			{
				return element;
			}
		}

		return source.Any() ? source.First() : default;
	}

	public static IList<T> Shuffle<T>(this IList<T> list, WasabiRandom random)
	{
		int n = list.Count;
		while (n > 1)
		{
			n--;
			int k = random.GetInt(0, n + 1);
			T value = list[k];
			list[k] = list[n];
			list[n] = value;
		}
		return list;
	}

	public static IList<T> ToShuffled<T>(this IEnumerable<T> list, WasabiRandom random)
	{
		return list.ToList().Shuffle(random);
	}

	public static bool NotNullAndNotEmpty<T>(this IEnumerable<T> source)
		=> source?.Any() is true;

	/// <summary>
	/// Generates all possible combinations of input <paramref name="items"/> with <paramref name="ofLength"/> length.
	/// </summary>
	/// <remarks>If you have numbers <c>1, 2, 3, 4</c>, then the output will contain <c>(2, 3, 4)</c> but not, for example, <c>(4, 3, 2)</c>.</remarks>
	public static IEnumerable<IEnumerable<T>> CombinationsWithoutRepetition<T>(
		this IEnumerable<T> items,
		int ofLength)
	{
		var itemsArr = items.ToArray();
		var templates = new Stack<(List<T> Result, ArraySegment<T> Items)>();
		templates.Push((new List<T>(), itemsArr));

		while (templates.Count > 0)
		{
			var (template, rest) = templates.Pop();
			if (template.Count == ofLength)
			{
				yield return template;
			}
			else if (template.Count + rest.Count >= ofLength)
			{
				for (var i = rest.Count - 1; i >= 0; i--)
				{
					var newTemplate = new List<T>(template) { rest[i] };
					templates.Push((newTemplate, rest[(i + 1)..]));
				}
			}
		}
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

	public static IOrderedEnumerable<SmartTransaction> OrderByBlockchain(this IEnumerable<SmartTransaction> me)
		=> me
			.OrderBy(x => x.Height)
			.ThenBy(x => x.BlockIndex)
			.ThenBy(x => x.FirstSeen);

	public static IOrderedEnumerable<TransactionSummary> OrderByBlockchain(this IEnumerable<TransactionSummary> me)
		=> me
			.OrderBy(x => x.Height)
			.ThenBy(x => x.BlockIndex)
			.ThenBy(x => x.FirstSeen);

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

	public static IEnumerable<T> TakeUntil<T>(this IEnumerable<T> list, Func<T, bool> predicate)
	{
		foreach (T el in list)
		{
			yield return el;
			if (predicate(el))
			{
				yield break;
			}
		}
	}

	public static IEnumerable<T> Singleton<T>(this T item)
	{
		yield return item;
	}

	public static double WeightedAverage<T>(this IEnumerable<T> source, Func<T, double> value, Func<T, double> weight)
	{
		return source.Select(x => value(x) * weight(x)).Sum() / source.Select(weight).Sum();
	}

	public static int MaxOrDefault(this IEnumerable<int> me, int defaultValue) =>
		me.DefaultIfEmpty(defaultValue).Max();
}
