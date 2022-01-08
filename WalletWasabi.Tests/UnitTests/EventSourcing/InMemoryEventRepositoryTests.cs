using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.Exceptions;
using WalletWasabi.Interfaces.EventSourcing;
using WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain;
using Xunit;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.EventSourcing;

public class InMemoryEventRepositoryTests : IDisposable
{
	private const string AggregateType = nameof(InMemoryEventRepositoryTests);

	/// <summary>Aggregate ID number one.</summary>
	private const string Id1 = "MY_ID_1";

	/// <summary>Aggregate ID number two.</summary>
	private const string Id2 = "MY_ID_2";

	private static readonly TimeSpan SemaphoreWaitTimeout = TimeSpan.FromSeconds(20);

	public InMemoryEventRepositoryTests(ITestOutputHelper output)
	{
		TestEventRepository = new TestInMemoryEventRepository(output);
		EventRepository = TestEventRepository;
	}

	private IEventRepository EventRepository { get; init; }
	private TestInMemoryEventRepository TestEventRepository { get; init; }

	[Fact]
	public async Task AppendEvents_Zero_Async()
	{
		WrappedEvent[] noEvents = Array.Empty<WrappedEvent>();

		await EventRepository.AppendEventsAsync(AggregateType, Id1, noEvents);

		IReadOnlyList<WrappedEvent> actualEvents = await EventRepository.GetEventsAsync(AggregateType, Id1);
		Assert.Empty(actualEvents);

		IReadOnlyList<string> actualAggregateIds = await EventRepository.GetAggregateIdsAsync(AggregateType);
		Assert.Empty(actualAggregateIds);
	}

	[Fact]
	public async Task AppendEvents_One_Async()
	{
		WrappedEvent[] events = new[]
		{
				new WrappedEvent(SequenceId: 1),
			};

		await EventRepository.AppendEventsAsync(AggregateType, Id1, events);

		IReadOnlyList<WrappedEvent> actualEvents = await EventRepository.GetEventsAsync(AggregateType, Id1);
		Assert.Equal(events, actualEvents);

		IReadOnlyList<string> actualAggregateIds = await EventRepository.GetAggregateIdsAsync(AggregateType);
		Assert.Equal(new string[] { Id1 }, actualAggregateIds);
	}

	[Fact]
	public async Task AppendEvents_Two_Async()
	{
		WrappedEvent[] events = new[]
		{
				new WrappedEvent(SequenceId: 1),
				new WrappedEvent(SequenceId: 2),
			};

		await EventRepository.AppendEventsAsync(AggregateType, Id1, events);

		IReadOnlyList<WrappedEvent> actualEvents = await EventRepository.GetEventsAsync(AggregateType, Id1);
		Assert.Equal(events, actualEvents);

		IReadOnlyList<string> aggregateIds = await EventRepository.GetAggregateIdsAsync(AggregateType);
		Assert.Equal(new string[] { Id1 }, aggregateIds);
	}

	[Fact]
	public async Task AppendEvents_NegativeSequenceId_Async()
	{
		WrappedEvent[] events = new[]
		{
				new WrappedEvent(SequenceId: -1)
			};

		async Task ActionAsync()
		{
			await EventRepository.AppendEventsAsync(AggregateType, Id1, events).ConfigureAwait(false);
		}

		ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(ActionAsync);
		Assert.Contains("First event sequenceId is not a positive number.", ex.Message);
	}

	[Fact]
	public async Task AppendEvents_SkippedSequenceId_Async()
	{
		WrappedEvent[] events = new[]
		{
				new WrappedEvent(2)
			};

		async Task ActionAsync()
		{
			await EventRepository.AppendEventsAsync(AggregateType, Id1, events).ConfigureAwait(false);
		}

		ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(ActionAsync);
		Assert.Contains("Invalid firstSequenceId (gap in sequence IDs) expected: '1' given: '2'", ex.Message);
	}

	[Fact]
	public async Task AppendEvents_OptimisticConcurrency_Async()
	{
		WrappedEvent[] events = new[]
		{
				new WrappedEvent(1)
			};

		await EventRepository.AppendEventsAsync(AggregateType, Id1, events);

		async Task ActionAsync()
		{
			await EventRepository.AppendEventsAsync(AggregateType, Id1, events).ConfigureAwait(false);
		}

		OptimisticConcurrencyException ex = await Assert.ThrowsAsync<OptimisticConcurrencyException>(ActionAsync);
		Assert.Contains("Conflict", ex.Message);
	}

	[Fact]
	public async Task AppendEventsAsync_Interleaving_Async()
	{
		WrappedEvent[] events_a_0 = new[] { new WrappedEvent(1) };
		WrappedEvent[] events_b_0 = new[] { new WrappedEvent(1) };
		WrappedEvent[] events_a_1 = new[] { new WrappedEvent(2) };
		WrappedEvent[] events_b_1 = new[] { new WrappedEvent(2) };

		await EventRepository.AppendEventsAsync(AggregateType, "a", events_a_0);
		await EventRepository.AppendEventsAsync(AggregateType, "b", events_b_0);
		await EventRepository.AppendEventsAsync(AggregateType, "a", events_a_1);
		await EventRepository.AppendEventsAsync(AggregateType, "b", events_b_1);

		IReadOnlyList<WrappedEvent> actualEventsA = await EventRepository.GetEventsAsync(AggregateType, "a");
		Assert.Equal(events_a_0.Concat(events_a_1), actualEventsA);

		IReadOnlyList<WrappedEvent> actualEventsB = await EventRepository.GetEventsAsync(AggregateType, "b");
		Assert.Equal(events_b_0.Concat(events_b_1), actualEventsB);

		IReadOnlyList<string> actualAggregateIds = await EventRepository.GetAggregateIdsAsync(AggregateType);
		Assert.Equal(new string[] { "a", "b" }, actualAggregateIds);
	}

	[Fact]
	public async Task AppendEventsAsync_InterleavingConflict_Async()
	{
		WrappedEvent[] events_a_0 = new[] { new WrappedEvent(1) };
		WrappedEvent[] events_b_0 = new[] { new WrappedEvent(1) };
		WrappedEvent[] events_a_1 = new[] { new WrappedEvent(2) };

		async Task ActionAsync()
		{
			await EventRepository.AppendEventsAsync(AggregateType, "a", events_a_0).ConfigureAwait(false);
			await EventRepository.AppendEventsAsync(AggregateType, "b", events_b_0).ConfigureAwait(false);
			await EventRepository.AppendEventsAsync(AggregateType, "a", events_a_1).ConfigureAwait(false);
			await EventRepository.AppendEventsAsync(AggregateType, "a", events_a_1).ConfigureAwait(false);
		}

		await Assert.ThrowsAsync<OptimisticConcurrencyException>(ActionAsync);

		IReadOnlyList<WrappedEvent> actualEventsA = await EventRepository.GetEventsAsync(AggregateType, "a");
		Assert.Equal(events_a_0.Concat(events_a_1), actualEventsA);

		IReadOnlyList<WrappedEvent> actualEventsB = await EventRepository.GetEventsAsync(AggregateType, "b");
		Assert.Equal(events_b_0, actualEventsB);

		IReadOnlyList<string> actualAggregateIds = await EventRepository.GetAggregateIdsAsync(AggregateType);
		Assert.Equal(new string[] { "a", "b" }, actualAggregateIds);
	}

	[Fact]
	public async Task AppendEvents_AppendIsAtomic_Async()
	{
		WrappedEvent[] events1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
		WrappedEvent[] events2 = new[] { new TestWrappedEvent(2, "b"), new TestWrappedEvent(3, "b") };

		async Task ActionAsync()
		{
			await EventRepository.AppendEventsAsync(AggregateType, Id1, events1).ConfigureAwait(false);
			await EventRepository.AppendEventsAsync(AggregateType, Id1, events2).ConfigureAwait(false);
		}

		await Assert.ThrowsAsync<OptimisticConcurrencyException>(ActionAsync);

		IReadOnlyList<WrappedEvent> wrappedEvents = await EventRepository.GetEventsAsync(AggregateType, Id1);
		Assert.Equal(events1, wrappedEvents);

		IReadOnlyList<string> actualAggregateIds = await EventRepository.GetAggregateIdsAsync(AggregateType);
		Assert.Equal(new string[] { Id1 }, actualAggregateIds);
	}

	[Fact]
	public async Task AppendEvents_CriticalSectionConflicts_Async()
	{
		WrappedEvent[] events1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
		WrappedEvent[] events2 = new[] { new TestWrappedEvent(2, "b"), new TestWrappedEvent(3, "b") };

		async Task Append1Async()
		{
			await EventRepository.AppendEventsAsync(AggregateType, Id1, events1);
		}

		async Task Append2Async()
		{
			bool entered = await TestEventRepository.AppendedSemaphore.WaitAsync(SemaphoreWaitTimeout).ConfigureAwait(false);
			Assert.True(entered, "Lock failed to be entered.");

			await EventRepository.AppendEventsAsync(AggregateType, Id1, events2);
		}

		async Task AppendInParallelAsync()
		{
			Task task1 = Task.Run(Append1Async);
			Task task2 = Task.Run(Append2Async);
			await Task.WhenAll(task1, task2);
		}

		async Task WaitForConflictAsync()
		{
			bool entered = await TestEventRepository.ConflictedSemaphore.WaitAsync(SemaphoreWaitTimeout).ConfigureAwait(false);
			Assert.True(entered, "Lock failed to be entered.");
		}

		TestEventRepository.AppendedCallbackAsync = WaitForConflictAsync;

		await Assert.ThrowsAsync<OptimisticConcurrencyException>(AppendInParallelAsync);
		IReadOnlyList<WrappedEvent> enumerable = await EventRepository.GetEventsAsync(AggregateType, Id1);
		Assert.Equal(events1, enumerable);

		IReadOnlyList<string> actualAggregateIds = await EventRepository.GetAggregateIdsAsync(AggregateType);
		Assert.Equal(new string[] { Id1 }, actualAggregateIds);
	}

	[Fact]
	public async Task AppendEvents_CriticalAppendConflicts_Async()
	{
		WrappedEvent[] events1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
		WrappedEvent[] events2 = new[] { new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b") };

		async Task Append1Async()
		{
			await EventRepository.AppendEventsAsync(AggregateType, Id1, events1).ConfigureAwait(false);
		}

		async Task Append2Async()
		{
			bool entered = await TestEventRepository.AppendedSemaphore.WaitAsync(SemaphoreWaitTimeout).ConfigureAwait(false);
			Assert.True(entered, "Lock failed to be entered.");

			await EventRepository.AppendEventsAsync(AggregateType, Id1, events2).ConfigureAwait(false);
		}

		async Task AppendInParallelAsync()
		{
			await Task.WhenAll(Append1Async(), Append2Async()).ConfigureAwait(false);
		}

		async Task WaitForNoConflictAsync()
		{
			bool entered = await TestEventRepository.ConflictedSemaphore.WaitAsync(SemaphoreWaitTimeout).ConfigureAwait(false);
			Assert.False(entered, "Lock failed to be entered.");
		}

		TestEventRepository.AppendedCallbackAsync = WaitForNoConflictAsync;

		// No conflict.
		await AppendInParallelAsync();

		IReadOnlyList<WrappedEvent> actualEvents = await EventRepository.GetEventsAsync(AggregateType, Id1);
		Assert.Equal(events1.Concat(events2), actualEvents);

		IReadOnlyList<string> actualAggregateIds = await EventRepository.GetAggregateIdsAsync(AggregateType);
		Assert.Equal(new string[] { Id1 }, actualAggregateIds);
	}

	[Theory]
	[InlineData(nameof(TestInMemoryEventRepository.ValidatedCallbackAsync))]
	[InlineData(nameof(TestInMemoryEventRepository.AppendedCallbackAsync))]
	public async Task ListEventsAsync_ConflictWithAppending_Async(string listOnCallback)
	{
		TestWrappedEvent[] events1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
		TestWrappedEvent[] events2 = new[] { new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b") };

		await EventRepository.AppendEventsAsync(AggregateType, Id1, events1);

		IReadOnlyList<WrappedEvent> result = Array.Empty<WrappedEvent>().ToList().AsReadOnly();

		Task ListCallbackAsync()
		{
			result = EventRepository.GetEventsAsync(AggregateType, Id1).Result;
			return Task.CompletedTask;
		}

		switch (listOnCallback)
		{
			case nameof(TestInMemoryEventRepository.ValidatedCallbackAsync):
				TestEventRepository.ValidatedCallbackAsync = ListCallbackAsync;
				break;

			case nameof(TestInMemoryEventRepository.AppendedCallbackAsync):
				TestEventRepository.AppendedCallbackAsync = ListCallbackAsync;
				break;

			default:
				throw new ApplicationException($"unexpected value listOnCallback: '{listOnCallback}'");
		}

		await EventRepository.AppendEventsAsync(AggregateType, Id1, events2);

		IEnumerable<TestWrappedEvent> expected = events1.AsEnumerable();

		switch (listOnCallback)
		{
			case nameof(TestInMemoryEventRepository.AppendedCallbackAsync):
				expected = expected.Concat(events2);
				break;
		}

		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(0, 2)]
	[InlineData(0, 3)]
	[InlineData(0, 4)]
	[InlineData(0, 5)]
	[InlineData(1, 1)]
	[InlineData(1, 2)]
	[InlineData(1, 3)]
	[InlineData(1, 4)]
	[InlineData(2, 1)]
	[InlineData(2, 2)]
	[InlineData(2, 3)]
	[InlineData(3, 1)]
	[InlineData(3, 2)]
	[InlineData(4, 1)]
	public async Task ListEventsAsync_OptionalArguments_Async(long afterSequenceId, int limit)
	{
		WrappedEvent[] events = new[]
		{
				new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a"),
				new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b")
			};

		await EventRepository.AppendEventsAsync(AggregateType, Id1, events);

		var result = await EventRepository.GetEventsAsync(
			AggregateType, Id1, afterSequenceId, limit);

		Assert.True(result.Count <= limit);
		Assert.True(result.All(a => afterSequenceId < a.SequenceId));
	}

	[Fact]
	public async Task ListAggregateIdsAsync_Async()
	{
		WrappedEvent[] events = new[]
		{
				new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a"),
				new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b")
			};

		await EventRepository.AppendEventsAsync(AggregateType, Id1, events);
		await EventRepository.AppendEventsAsync(AggregateType, "MY_ID_2", events);

		IReadOnlyList<string> actualAggregateIds = await EventRepository.GetAggregateIdsAsync(AggregateType);
		Assert.Equal(new string[] { Id1, "MY_ID_2" }, actualAggregateIds);
	}

	[Theory]
	[InlineData("0", 0)]
	[InlineData("0", 1)]
	[InlineData("0", 2)]
	[InlineData("0", 3)]
	[InlineData(Id1, 0)]
	[InlineData(Id1, 1)]
	[InlineData(Id1, 2)]
	[InlineData("MY_ID_2", 0)]
	[InlineData("MY_ID_2", 1)]
	[InlineData("3", 0)]
	[InlineData("3", 1)]
	public async Task ListAggregateIdsAsync_OptionalArguments_Async(string afterAggregateId, int limit)
	{
		WrappedEvent[] events = new[]
		{
				new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a"),
				new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b")
			};

		await EventRepository.AppendEventsAsync(AggregateType, Id1, events);
		await EventRepository.AppendEventsAsync(AggregateType, Id2, events);

		IReadOnlyList<string> result = await EventRepository.GetAggregateIdsAsync(AggregateType, afterAggregateId, limit);

		Assert.True(result.Count <= limit);
		Assert.True(result.All(a => afterAggregateId.CompareTo(a) <= 0));
	}

	public void Dispose()
	{
		TestEventRepository.Dispose();
	}
}
