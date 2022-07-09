using System.Collections.Generic;

namespace WalletWasabi.WabiSabi.Client;

public static class LinqEx
{
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
}
