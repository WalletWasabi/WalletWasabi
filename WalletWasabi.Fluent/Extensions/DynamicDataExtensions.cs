using System.Collections.Generic;
using DynamicData;

namespace WalletWasabi.Fluent.Extensions;

public static class DynamicDataExtensions
{
	public static IDisposable RefillFrom<TObject, TKey>(this ISourceCache<TObject, TKey> sourceCache, IObservable<IEnumerable<TObject>> contents) where TKey : notnull
	{
		return contents.Subscribe(list => sourceCache.Edit(updater => updater.Load(list)));
	}

	public static IObservable<IReadOnlyCollection<TObject>> ToCollectionStartWithEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> changeSet) where TKey : notnull where TObject : notnull
	{
		return changeSet.ToCollection().StartWithEmpty();
	}

	public static IObservable<IReadOnlyCollection<TObject>> ToCollectionStartWithEmpty<TObject>(this IObservable<IChangeSet<TObject>> changeSet) where TObject : notnull
	{
		return changeSet.ToCollection().StartWithEmpty();
	}
}
