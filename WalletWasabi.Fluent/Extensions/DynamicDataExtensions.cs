using System.Collections.Generic;
using System.Reactive.Linq;
using DynamicData;

namespace WalletWasabi.Fluent.Extensions;

public static class DynamicDataExtensions
{
	public static IDisposable RefillFrom<TObject, TKey>(this ISourceCache<TObject, TKey> sourceCache, IObservable<IEnumerable<TObject>> contents) where TKey : notnull
	{
		return contents.Subscribe(list => sourceCache.Edit(updater => updater.Load(list)));
	}

	public static IObservable<bool> HasAny<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> changeSet) where TKey : notnull
	{
		return changeSet.AsObservableCache().CountChanged.Select(n => n > 0);
	}
}
