using System.Collections.Generic;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Kernel;
using ReactiveUI;

namespace WalletWasabi.Fluent.Extensions;

public static class DynamicDataExtensions
{
	/// <summary>
	///     Transforms the items, and when an update is received, allows the preservation of the previous view model
	/// </summary>
	/// <typeparam name="TObject">The type of the object.</typeparam>
	/// <typeparam name="TKey">The type of the key.</typeparam>
	/// <typeparam name="TDestination">The type of the destination.</typeparam>
	/// <param name="source">The source.</param>
	/// <param name="transformFactory">The transform factory.</param>
	/// <param name="updateAction">
	///     Apply changes to the original. Example (previousTransformedItem, newOriginalItem) =>
	///     previousTransformedItem.Value = newOriginalItem
	/// </param>
	/// <returns>The resulting observable changeset</returns>
	public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TObject, TKey, TDestination>(
		this IObservable<IChangeSet<TObject, TKey>> source,
		Func<TObject, TDestination> transformFactory,
		Action<TDestination, TObject>? updateAction = null) where TKey : notnull
	{
		return source.Scan(
			(ChangeAwareCache<TDestination, TKey>?)null,
			(cache, changes) =>
			{
				cache ??= new ChangeAwareCache<TDestination, TKey>(changes.Count);

				foreach (var change in changes)
				{
					switch (change.Reason)
					{
						case ChangeReason.Add:
							cache.AddOrUpdate(transformFactory(change.Current), change.Key);
							break;

						case ChangeReason.Update:
							if (updateAction == null)
							{
								continue;
							}

							var previous = cache.Lookup(change.Key).ValueOrThrow(
								() => new MissingKeyException($"{change.Key} is not found."));

							updateAction(previous, change.Current);

							// send a refresh as this will force downstream operators
							cache.Refresh(change.Key);
							break;

						case ChangeReason.Remove:
							cache.Remove(change.Key);
							break;

						case ChangeReason.Refresh:
							cache.Refresh(change.Key);
							break;

						case ChangeReason.Moved:
							// Do nothing !
							break;
					}
				}

				return cache;
			}).Select(cache => cache!.CaptureChanges());
	}

	public static IDisposable RefillFrom<TObject, TKey>(this ISourceCache<TObject, TKey> sourceCache, IObservable<IEnumerable<TObject>> contents) where TKey : notnull
	{
		return contents.Subscribe(list => sourceCache.Edit(updater => updater.Load(list)));
	}

	public static IObservable<bool> HasAny<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> changeSet) where TKey : notnull
	{
		return changeSet.AsObservableCache().CountChanged.Select(n => n > 0);
	}
}
