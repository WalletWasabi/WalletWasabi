using System.Collections.Generic;

namespace WalletWasabi.Fluent.Helpers;

public static class Sort<T>
{
	public static Comparison<T?> Ascending<TKey>(Func<T, TKey> selector)
	{
		return (left, right) => Compare(left, right, selector);
	}

	public static Comparison<T?> Descending<TKey>(Func<T, TKey> selector)
	{
		return (left, right) => Compare(right, left, selector);
	}

	private static int Compare<TKey>(T? left, T? right, Func<T, TKey> selector)
	{
		if (left is null)
		{
			return right is null ? 0 : -1;
		}

		if (right is null)
		{
			return 1;
		}

		return Comparer<TKey>.Default.Compare(selector(left), selector(right));
	}
}
