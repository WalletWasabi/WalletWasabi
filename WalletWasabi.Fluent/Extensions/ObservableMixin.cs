using System.Collections.Generic;
using System.Reactive.Linq;
using DynamicData;

namespace WalletWasabi.Fluent.Extensions;

public static class ObservableMixin
{
	public static IDisposable AddOrUpdate<TObject, TKey>(
		this ISourceCache<TObject, TKey> sourceCache,
		IObservable<IEnumerable<TObject>> contents) where TKey : notnull
	{
		return contents.Subscribe(list => sourceCache.Edit(updater => updater.AddOrUpdate(list)));
	}

	public static IObservable<T> ReplayLastActive<T>(this IObservable<T> observable)
	{
		return observable.Replay(1).RefCount();
	}
}
