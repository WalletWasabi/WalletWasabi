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
		var running = 0;
		var maxConcurrent = 0;
		var runs = 0;
		var results = new List<int>();

		using var subscription = signal
			.FetchLatestAsync(async cancellationToken =>
			{
				var now = Interlocked.Increment(ref running);
				InterlockedMax(ref maxConcurrent, now);
				await Task.Delay(100, cancellationToken);
				Interlocked.Decrement(ref running);
				return Interlocked.Increment(ref runs);
			})
			.Subscribe(x => { lock (results) { results.Add(x); } });

		// Signals arrive much faster than a fetch takes (the rescan case). With Switch every fetch was cancelled.
		for (var i = 0; i < 50; i++)
		{
			signal.OnNext(Unit.Default);
			await Task.Delay(10);
		}

		await Task.Delay(400);

		lock (results)
		{
			Assert.NotEmpty(results);
			// Signals during a run collapse into one follow-up run instead of 50 runs.
			Assert.InRange(results.Count, 2, 20);
		}
		Assert.Equal(1, maxConcurrent);
	}

	[Fact]
	public async Task FailedFetchDoesNotEndTheSequenceAsync()
	{
		using var signal = new Subject<Unit>();
		var calls = 0;
		var results = new List<int>();
		Exception? error = null;

		using var subscription = signal
			.FetchLatestAsync(_ =>
			{
				var call = Interlocked.Increment(ref calls);
				return call == 1 ? Task.FromException<int>(new InvalidOperationException("boom")) : Task.FromResult(call);
			})
			.Subscribe(x => { lock (results) { results.Add(x); } }, ex => error = ex);

		signal.OnNext(Unit.Default);
		await Task.Delay(100);
		signal.OnNext(Unit.Default);
		await Task.Delay(100);

		Assert.Null(error);
		lock (results)
		{
			Assert.Equal([2], results);
		}
	}

	private static void InterlockedMax(ref int target, int value)
	{
		int current;
		while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
		{
		}
	}
}
