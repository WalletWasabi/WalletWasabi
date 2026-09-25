using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Extensions;

public static class ObservableExtensions
{
	public static IDisposable SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNextAsync) =>
		source
			.Select(x => Observable.FromAsync(() => onNextAsync(x)))
			.Concat()
			.Subscribe();

	public static IObservable<Unit> DoAsync<T>(this IObservable<T> source, Func<T, Task> onNextAsync) =>
		source
			.Select(x => Observable.FromAsync(() => onNextAsync(x)))
			.Concat();

	public static IObservable<Unit> ToSignal<T>(this IObservable<T> source) => source.Select(_ => Unit.Default);

	public static IObservable<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> WhenAnyValue<TSender, T1, T2, T3, T4, T5, T6, T7, T8, T9>(
			this TSender sender,
						Expression<Func<TSender, T1>> property1,
						Expression<Func<TSender, T2>> property2,
						Expression<Func<TSender, T3>> property3,
						Expression<Func<TSender, T4>> property4,
						Expression<Func<TSender, T5>> property5,
						Expression<Func<TSender, T6>> property6,
						Expression<Func<TSender, T7>> property7,
						Expression<Func<TSender, T8>> property8,
						Expression<Func<TSender, T9>> property9)
	{
		return sender.WhenAny(
			property1,
			property2,
			property3,
			property4,
			property5,
			property6,
			property7,
			property8,
			property9,
			(c1, c2, c3, c4, c5, c6, c7, c8, c9) => (c1.Value, c2.Value, c3.Value, c4.Value, c5.Value, c6.Value, c7.Value, c8.Value, c9.Value));
	}

	public static IObservable<(T1, T2, T3)> Flatten<T1, T2, T3>(this IObservable<((T1, T2), T3)> source) =>
		source.Select(t => (t.Item1.Item1, t.Item1.Item2, t.Item2));

	public static IObservableCache<TObject, TKey> Fetch<TObject, TKey>(this IObservable<Unit> signal, Func<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer = null)
		where TKey : notnull where TObject : notnull
	{
		return signal.Select(_ => source())
					 .EditDiff(keySelector, equalityComparer)
					 .DisposeMany()
					 .AsObservableCache();
	}

	public static IObservableCache<TObject, TKey> FetchAsync<TObject, TKey>(
		this IObservable<Unit> signal,
		Func<CancellationToken, Task<IEnumerable<TObject>>> source,
		Func<TObject, TKey> keySelector,
		IEqualityComparer<TObject>? equalityComparer = null)
		where TKey : notnull where TObject : notnull
	{
		return signal.FetchLatestAsync(source)
			.EditDiff(keySelector, equalityComparer)
			.DisposeMany()
			.AsObservableCache();
	}

	/// <summary>
	/// Runs <paramref name="source"/> once per signal, one run at a time. Signals that arrive while a run is in
	/// progress collapse into a single follow-up run, so slow fetches (e.g. history rebuilds during a rescan)
	/// neither pile up in parallel nor get cancelled before they can finish. A failed run is logged and the
	/// next signal retries; it does not terminate the sequence.
	/// </summary>
	public static IObservable<T> FetchLatestAsync<T>(this IObservable<Unit> signal, Func<CancellationToken, Task<T>> source)
	{
		return Observable.Create<T>(observer =>
		{
			var cts = new CancellationTokenSource();
			var cancellationToken = cts.Token;

			// Capacity 1 with DropWrite: at most one run is pending while another one is running.
			var pending = Channel.CreateBounded<Unit>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
			var subscription = signal.Subscribe(
				x => pending.Writer.TryWrite(x),
				ex => pending.Writer.TryComplete(ex),
				() => pending.Writer.TryComplete());

			_ = Task.Run(async () =>
			{
				try
				{
					await foreach (var _ in pending.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
					{
						try
						{
							observer.OnNext(await source(cancellationToken).ConfigureAwait(false));
						}
						catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
						{
							return;
						}
						catch (Exception ex)
						{
							Logger.LogError(ex);
						}
					}

					observer.OnCompleted();
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
				}
				catch (Exception ex)
				{
					observer.OnError(ex);
				}
			});

			return Disposable.Create(() =>
			{
				subscription.Dispose();
				cts.Cancel();
			});
		});
	}
}
