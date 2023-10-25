using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
#pragma warning disable CA2000

namespace WalletWasabi.Fluent.Helpers;

public class Retriever<TObject, TKey> : IDisposable where TKey : notnull
{
	private readonly CompositeDisposable _disposable = new();

	public Retriever(IObservable<Unit> retrieveSignal, Func<TObject, TKey> keySelector, Func<IEnumerable<TObject>> retrieve)
	{
		var sourceCache = new SourceCache<TObject, TKey>(keySelector)
			.DisposeWith(_disposable);

		retrieveSignal
			.Select(_ => retrieve()).Do(items => sourceCache.Edit(updater => updater.Load(items)))
			.Subscribe()
			.DisposeWith(_disposable);

		Changes = sourceCache.Connect();
	}

	public IObservable<IChangeSet<TObject, TKey>> Changes { get; }

	public void Dispose() => _disposable.Dispose();
}
