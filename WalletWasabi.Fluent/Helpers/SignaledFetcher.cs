using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;

namespace WalletWasabi.Fluent.Helpers;

/// <summary>
///     Keeps a cache updated by applying a final list. Uses diffs to update the cache in an optimized way.
/// </summary>
/// <typeparam name="TObject">Type of the cache</typeparam>
/// <typeparam name="TKey">Key of the cache</typeparam>
public class SignaledFetcher<TObject, TKey> : IDisposable where TKey : notnull where TObject : notnull
{
	private readonly CompositeDisposable _disposable = new();

	/// <summary>
	///     Creates an instance of <see cref="SignaledFetcher{TObject,TKey}" /> that updates its contents each time the
	///     <paramref name="retrieveSignal" /> emits a signal. The final list of elements in the cache are fetched using the
	///     <paramref name="retrieve" /> method. The <paramref name="keySelector"/> is used to locate the key of each object of <typeparamref name="TObject"/>
	/// </summary>
	/// <param name="retrieveSignal">Observable that triggers the updates</param>
	/// <param name="keySelector">Key selector function </param>
	/// <param name="retrieve">The retriever function. Retrieves the full updates list (final list)</param>
	public SignaledFetcher(IObservable<Unit> retrieveSignal, Func<TObject, TKey> keySelector, Func<IEnumerable<TObject>> retrieve)
	{
		Cache = retrieveSignal
			.Select(_ => retrieve())
			.EditDiff(keySelector)
			.AsObservableCache()
			.DisposeWith(_disposable);
	}

	public IObservableCache<TObject, TKey> Cache { get; }

	public void Dispose() => _disposable.Dispose();
}
