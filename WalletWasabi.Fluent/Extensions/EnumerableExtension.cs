using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Extensions;

public static class EnumerableExtensions
{
	public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> enumerable) =>
		enumerable
			.Where(x => x is not null)
			.Select(x => x!);

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

	public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> enumerable)
	{
		foreach (var item in enumerable)
		{
			yield return item;
		}
	}

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
}
