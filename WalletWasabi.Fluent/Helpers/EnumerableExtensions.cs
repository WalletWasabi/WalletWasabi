using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Helpers;

public static class EnumerableExtensions
{
	/// <summary>
	/// Splits the collection into two collections, containing the elements for which the given predicate returns True and False respectively. Element order is preserved in both of the created lists.
	/// </summary>
	public static (IEnumerable<T>, IEnumerable<T>) Partition<T>(this IEnumerable<T> me, Predicate<T> predicate)
	{
		var trueList = new List<T>();
		var falseList = new List<T>();

		foreach (var item in me)
		{
			if (predicate(item))
			{
				trueList.Add(item);
			}
			else
			{
				falseList.Add(item);
			}
		}

		return (trueList, falseList);
	}
}
