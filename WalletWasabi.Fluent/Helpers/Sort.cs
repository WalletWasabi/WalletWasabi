using System.Collections.Generic;

namespace WalletWasabi.Fluent.Helpers;

public static class Sort<T>
{
	public static Comparison<T?> Ascending<TKey>(Func<T, TKey> selector, IComparer<TKey>? comparer = null)
	{
		return (left, right) => Compare(left, right, selector, comparer);
	}

	public static Comparison<T?> Descending<TKey>(Func<T, TKey> selector, IComparer<TKey>? comparer = null)
	{
		return (left, right) => Compare(right, left, selector, comparer);
	}

	private static int Compare<TKey>(T? left, T? right, Func<T, TKey> selector, IComparer<TKey>? comparer)
	{
		if (left is null)
		{
			return right is null ? 0 : -1;
		}

		if (right is null)
		{
			return 1;
		}

		var leftKey = selector(left);
		var rightKey = selector(right);

		return
			comparer?.Compare(leftKey, rightKey) ??
			Comparer<TKey>.Default.Compare(leftKey, rightKey);
	}
}
