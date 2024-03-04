using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Extensions;

public static class EnumerableExtensions
{
	public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> enumerable) =>
		enumerable
			.Where(x => x is not null)
			.Select(x => x!);

	public static IEnumerable<T> Delimit<T>(this IEnumerable<T> source, T delimiter)
	{
		foreach (T item in source.Take(1))
		{
			yield return item;
		}
		
		foreach (T item in source.Skip(1))
		{
			yield return delimiter;
			yield return item;
		}
	}
}
