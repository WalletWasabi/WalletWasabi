using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Extensions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class FetchLatestAsyncTests
{
	[Fact]
	public async Task SlowFetchCompletesWhileSignalsKeepArrivingAsync()
	{
		using var signal = new Subject<Unit>();
		var timeout = TimeSpan.FromSeconds(10);
		var running = 0;
		var maxConcurrent = 0;
		var runs = 0;
		var results = new List<int>();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Each fetch announces that it started and then waits until the test releases it, so no timing is involved.
		using var started = new SemaphoreSlim(0);
		using var release = new SemaphoreSlim(0);

		using var subscription = signal
			.FetchLatestAsync(async cancellationToken =>
			{
				InterlockedMax(ref maxConcurrent, Interlocked.Increment(ref running));
				started.Release();
				await release.WaitAsync(cancellationToken);
				Interlocked.Decrement(ref running);
				return Interlocked.Increment(ref runs);
			})
			.Subscribe(x => { lock (results) { results.Add(x); } }, () => completed.SetResult());

		signal.OnNext(Unit.Default);
		Assert.True(await started.WaitAsync(timeout));

		// Signals keep arriving while the fetch is slow (the rescan case). With Switch every fetch was cancelled.
		for (var i = 0; i < 50; i++)
		{
			signal.OnNext(Unit.Default);
		}

		// The running fetch finishes, and the 50 signals collapse into exactly one follow-up fetch.
		release.Release();
		Assert.True(await started.WaitAsync(timeout));
		release.Release();

		signal.OnCompleted();
		await completed.Task.WaitAsync(timeout);

		lock (results)
		{
			Assert.Equal([1, 2], results);
		}
		Assert.Equal(1, maxConcurrent);
	}

	[Fact]
	public async Task FailedFetchDoesNotEndTheSequenceAsync()
	{
		using var signal = new Subject<Unit>();
		var calls = 0;
		var firstFetchStarted = new TaskCompletionSource();
		var nextResult = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
		Exception? error = null;

		using var subscription = signal
			.FetchLatestAsync(_ =>
			{
				var call = Interlocked.Increment(ref calls);
				if (call == 1)
				{
					firstFetchStarted.SetResult();
					return Task.FromException<int>(new InvalidOperationException("boom"));
				}

				return Task.FromResult(call);
			})
			.Subscribe(x => nextResult.TrySetResult(x), ex => error = ex);

		signal.OnNext(Unit.Default);

		// Once the first fetch has started, its signal has been taken, so the next one is not coalesced into it.
		await firstFetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
		signal.OnNext(Unit.Default);

		Assert.Equal(2, await nextResult.Task.WaitAsync(TimeSpan.FromSeconds(10)));
		Assert.Null(error);
	}

	private static void InterlockedMax(ref int target, int value)
	{
		int current;
		while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
		{
		}
	}
}
