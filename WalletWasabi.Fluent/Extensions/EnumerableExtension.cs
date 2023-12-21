using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Extensions;

public static class EnumerableExtensions
{
	public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> enumerable) =>
		enumerable
			.Where(x => x is not null)
			.Select(x => x!);
}
