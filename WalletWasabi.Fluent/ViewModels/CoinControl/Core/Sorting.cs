using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public static class Sorting
{
	public static Comparison<TSource?> SortAscending<TSource, TProperty>(Func<TSource, TProperty> selector)
	{
		return (x, y) => Comparer<TProperty>.Default.Compare(selector(x!), selector(y!));
	}

	public static Comparison<TSource?> SortDescending<TSource, TProperty>(Func<TSource, TProperty?> selector)
	{
		return (x, y) => Comparer<TProperty>.Default.Compare(selector(y!), selector(x!));
	}
}
