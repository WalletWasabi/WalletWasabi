using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

/// <summary>
/// Notebook: https://github.com/lontivero/DecompositionsPlayground/blob/master/Notebook.ipynb
/// </summary>
public static class Decomposer
{
	public static IEnumerable<(long Sum, int Count, ulong Decomposition)> Decompose(long target, long tolerance, int maxCount, long[] stdDenoms)
	{
		if (maxCount is <= 1 or > 8)
		{
			throw new ArgumentOutOfRangeException(nameof(maxCount), "The maximum decomposition length cannot be greater than 8 or smaller than 2.");
		}
		if (target <= 0)
		{
			throw new ArgumentException("Only positive numbers can be decomposed.", nameof(target));
		}

		var denoms = stdDenoms.SkipWhile(x => x > target).ToArray();

		if (denoms.Length > 255)
		{
			throw new ArgumentException("Too many denominations. Maximum number is 255.", nameof(target));
		}
		return denoms.SelectMany(_ => InternalCombinations(target, tolerance: tolerance, maxCount, denoms)).Take(10_000).ToList();
	}

	private static IEnumerable<(long Sum, int Count, ulong Decomposition)> InternalCombinations(long target, long tolerance, int maxLength, long[] denoms)
	{
		IEnumerable<(long Sum, int Count, ulong Decomposition)> Combinations(
			int currentDenominationIdx,
			ulong accumulator,
			long sum,
			int k)
		{
			accumulator = accumulator << 8 | (ulong)currentDenominationIdx & 0xff;
			var currentDenomination = denoms[currentDenominationIdx];
			sum += currentDenomination;
			var remaining = target - sum;
			if (k == 0 || remaining < tolerance)
			{
				return new[] { (sum, maxLength - k, accumulator) };
			}

			currentDenominationIdx = Search(remaining, denoms, currentDenominationIdx);

			return Enumerable.Range(0, denoms.Length - currentDenominationIdx)
				.TakeWhile(i => k * denoms[currentDenominationIdx + i] >= remaining - tolerance)
				.SelectMany((_, i) =>
					Combinations(currentDenominationIdx + i, accumulator, sum, k - 1)
					.TakeUntil(x => x.Sum == target));
		}

		return denoms.SelectMany((_, i) => Combinations(i, 0ul, 0, maxLength - 1)).Take(5_000).ToList();
	}

	private static int Search(long value, long[] denoms, int offset)
	{
		var startingIndex = Array.BinarySearch(denoms, offset, denoms.Length - offset, value, ReverseComparer.Default);
		return startingIndex < 0 ? ~startingIndex : startingIndex;
	}

	public static IEnumerable<long> ToRealValuesArray(ulong decomposition, int count, long[] denoms)
	{
		var list = new long[count];
		for (var i = 0; i < count; i++)
		{
			var index = decomposition >> i * 8 & 0xff;
			list[count - i - 1] = denoms[index];
		}
		return list;
	}
}
