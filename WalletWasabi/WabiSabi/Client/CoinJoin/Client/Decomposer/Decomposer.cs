using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

/// <summary>
/// Notebook: https://github.com/lontivero/DecompositionsPlayground/blob/master/Notebook.ipynb
/// </summary>
public static class Decomposer
{
	public static IEnumerable<IDecomposition> Decompose(long target, long tolerance, int maxCount, long[] denoms)
	{
		if (target <= 0)
		{
			throw new ArgumentException("Only positive numbers can be decomposed.", nameof(target));
		}

		if (tolerance <= 0 || tolerance >= target)
		{
			throw new ArgumentException("Tolerance must be greater than zero and less than the target.",
				nameof(tolerance));
		}

		if (maxCount < 0)
		{
			throw new ArgumentException("MaxCount must be greater than or equal to zero.", nameof(maxCount));
		}

		return InternalCombinations(new NullDecomposition(), 0, target, tolerance: tolerance, maxCount, denoms).Take(10_000).ToList();
	}

	private static IEnumerable<IDecomposition> InternalCombinations(IDecomposition head, long sum, long target, long tolerance, int k, long[] denoms)
	{
		var remaining = target - sum;
		if (k == 0 || remaining < tolerance)
		{
			return [head];
		}

		var newDenoms = denoms[Search(remaining, denoms)..];
		return newDenoms
			.TakeWhile(d => k * d >= remaining - tolerance)
			.SelectMany((d, i) => InternalCombinations(new Decomposition(d, head), sum + d, target, tolerance, k - 1, newDenoms[i..])
				.TakeUntil(x => x.Sum == target));
	}

	static int Search(long value, long[] denoms)
	{
		var startingIndex = Array.BinarySearch(denoms, 0, denoms.Length, value, ReverseComparer.Default);
		return startingIndex < 0 ? ~startingIndex : startingIndex;
	}

	public interface IDecomposition
	{
		long Sum { get; }
		IEnumerable<long> AsEnumerable();
	}

	private record Decomposition(long V, IDecomposition Next) : IDecomposition
	{
		public long Sum => V + Next.Sum;
		public IEnumerable<long> AsEnumerable() => Next.AsEnumerable().Append(V);
	}

	private record NullDecomposition : IDecomposition
	{
		public long Sum => 0;
		public IEnumerable<long> AsEnumerable() => Array.Empty<long>();
	}
}

