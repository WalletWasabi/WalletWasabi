using System.Collections.Generic;
using WalletWasabi.Helpers;

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
}
