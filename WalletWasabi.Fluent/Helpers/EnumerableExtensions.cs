using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Helpers
{
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
			Func<TSource, DateTimeOffset> timeSampler, Func<TSource, TResult> sampler,
			TimeSpan interval, DateTimeOffset endTime, DateTimeOffset? startFrom = default)
		{
			var source = sourceData.ToArray();

			if (!source.Any())
			{
				yield break;
			}

			var currentTime = startFrom ?? timeSampler(source.First());

			var lastFound = startFrom is { }
				? (timestamp: currentTime, result: sampler(source.FirstOrDefault(x=>timeSampler(x) <= currentTime)!))
				: (timestamp: currentTime, result: sampler(source.First()));

			yield return lastFound;

			currentTime -= interval;

			while (currentTime > endTime)
			{
				var current = source.FirstOrDefault(x => timeSampler(x) <= currentTime);

				if (current is { })
				{
					lastFound = (currentTime, sampler(current));

					yield return lastFound;
				}
				else
				{
					yield break;
				}

				currentTime -= interval;
			}
		}
	}
}