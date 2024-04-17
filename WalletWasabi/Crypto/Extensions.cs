using System.Collections.Generic;
using WalletWasabi.Helpers;
using NBitcoin.Secp256k1;
using NBitcoin;
using System.Linq;

namespace WalletWasabi.Crypto;

public static class Extensions
{
	public static IEnumerable<TResult> Zip<TFirst, TSecond, TThird, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, IEnumerable<TThird> third, Func<TFirst, TSecond, TThird, TResult> resultSelector)
	{
		Guard.NotNull(nameof(first), first);
		Guard.NotNull(nameof(second), second);
		Guard.NotNull(nameof(third), third);
		Guard.NotNull(nameof(resultSelector), resultSelector);
		using var e1 = first.GetEnumerator();
		using var e2 = second.GetEnumerator();
		using var e3 = third.GetEnumerator();
		while (e1.MoveNext() && e2.MoveNext() && e3.MoveNext())
		{
			yield return resultSelector(e1.Current, e2.Current, e3.Current);
		}
	}

	public static double Median(this IEnumerable<double> me)
	{
		if (!me.Any())
		{
			return 0;
		}
		var sorted = me.OrderBy(x => x).ToArray();
		return sorted[sorted.Length / 2];
	}

	public static double StdDev(this IEnumerable<double> values)
	{
		var mean = values.Average();
		var squaresSum = values
			.Select(x => x - mean)
			.Select(x => x * x)
			.Sum();

		return Math.Sqrt(squaresSum / values.Count());
	}
}
