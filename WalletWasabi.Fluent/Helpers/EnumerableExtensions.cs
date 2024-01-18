using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Helpers;

public static class EnumerableExtensions
{
	/// <summary>
	/// Creates a time sampled dataset from a source dataset.
	/// </summary>
	/// <param name="sourceData">Enumerable of source data, in reverse chronological order. i.e. newest data point first.</param>
	/// <param name="timeSampler">An expression that determines the timestamp of an entry in the source.</param>
	/// <param name="sampler">An expression that samples or selects the data from an entry.</param>
	/// <param name="interval">The timespan between each sample.</param>
	/// <param name="endTime">The oldest time offset where the sampling will end.</param>
	/// <param name="startFrom">The time to start from.</param>
	/// <typeparam name="TSource">The type of the elements in the dataset.</typeparam>
	/// <typeparam name="TResult">The type of the sampled data.</typeparam>
	/// <returns>A new IEnumerable if the Selected data at each sample point.</returns>
	public static IEnumerable<(DateTimeOffset timestamp, TResult result)> SelectTimeSampleBackwards<TSource, TResult>(
		this IEnumerable<TSource> sourceData,
		Func<TSource, DateTimeOffset> timeSampler,
		Func<TSource, TResult> sampler,
		TimeSpan interval,
		DateTimeOffset endTime,
		TResult defaultValue,
		DateTimeOffset? startFrom = default)
	{
		var source = sourceData.ToArray();

		if (!source.Any())
		{
			yield break;
		}

		var currentTime = startFrom ?? timeSampler(source.First());

		var lastFound = startFrom is { }
			? (timestamp: currentTime, result: sampler(source.FirstOrDefault(x => timeSampler(x) <= currentTime)!))
			: (timestamp: currentTime, result: sampler(source.First()));

		yield return lastFound;

		currentTime -= interval;

		while (currentTime > endTime)
		{
			var current = source.FirstOrDefault(x => timeSampler(x) <= currentTime);

			lastFound = current is { } ? (currentTime, sampler(current)) : (currentTime, defaultValue);

			yield return lastFound;

			currentTime -= interval;
		}
	}

	public static int LastIndexOf<T>(this IEnumerable<T> source, T itemToFind)
	{
		return LastIndexOf(source, itemToFind, EqualityComparer<T>.Default);
	}

	public static int LastIndexOf<T>(this IEnumerable<T> source, T itemToFind, IEqualityComparer<T> equalityComparer)
	{
		if (source is null)
		{
			throw new ArgumentNullException(nameof(source));
		}

		var sourceArray = source.ToArray();

		for (var i = sourceArray.Length - 1; i >= 0; i--)
		{
			var currentItem = sourceArray[i];

			if (equalityComparer.Equals(currentItem, itemToFind))
			{
				return i;
			}
		}

		return -1;
	}

	public static bool IsEmpty<T>(this IEnumerable<T> source) => !source.Any();

	/// <summary>
	/// Splits the collection into two collections, containing the elements for which the given predicate returns True and False respectively. Element order is preserved in both of the created lists.
	/// </summary>
	public static (IEnumerable<T>, IEnumerable<T>) Partition<T>(this IEnumerable<T> me, Predicate<T> predicate)
	{
		var trueList = new List<T>();
		var falseList = new List<T>();

		foreach (var item in me)
		{
			if (predicate(item))
			{
				trueList.Add(item);
			}
			else
			{
				falseList.Add(item);
			}
		}

		return (trueList, falseList);
	}
}
