using System.Collections.Generic;
using System.Reactive.Linq;
using DynamicData;

namespace WalletWasabi.Fluent.Extensions;

public static class DynamicDataExtensions
{
	public static IDisposable RefillFrom<TObject, TKey>(this ISourceCache<TObject, TKey> sourceCache, IObservable<IEnumerable<TObject>> contents) where TKey : notnull where TObject : notnull
	{
		return contents.Subscribe(list => sourceCache.Edit(updater => updater.Load(list)));
	}

	public static IObservable<bool> NotEmpty<TObject, TKey>(this IObservableCache<TObject, TKey> changeSet) where TKey : notnull where TObject : notnull
	{
		return changeSet.CountChanged.Select(n => n > 0);
	}

	public static IObservable<bool> Empty<TObject, TKey>(this IObservableCache<TObject, TKey> changeSet) where TKey : notnull where TObject : notnull
	{
		return changeSet.CountChanged.Select(n => n == 0);
	}
}
