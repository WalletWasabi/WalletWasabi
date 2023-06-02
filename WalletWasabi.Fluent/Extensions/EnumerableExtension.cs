using System.Collections.Generic;
using System.Linq;

public static class EnumerableExtensions
{
	public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> observable) =>
		observable
			.Where(x => x is not null)
			.Select(x => x!);
}
