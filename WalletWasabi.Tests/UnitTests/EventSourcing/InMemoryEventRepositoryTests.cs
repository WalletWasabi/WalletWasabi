using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.Exceptions;
using WalletWasabi.Extensions;
using WalletWasabi.Interfaces.EventSourcing;
using WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain;
using Xunit;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.EventSourcing
{
	public class InMemoryEventRepositoryTests
	{
		private const string TestRoundAggregate = "TestRoundAggregate";
		private readonly TimeSpan _semaphoreWaitTimeout = TimeSpan.FromSeconds(1);

		private IEventRepository EventRepository { get; init; }
		private TestInMemoryEventRepository TestEventRepository { get; init; }

		private ITestOutputHelper Output { get; init; }

		public InMemoryEventRepositoryTests(ITestOutputHelper output)
		{
			Output = output;
			TestEventRepository = new TestInMemoryEventRepository(output);
			EventRepository = TestEventRepository;
		}

		[Fact]
		public async Task AppendEvents_Zero_Async()
		{
			// Arrange
			var events = Array.Empty<WrappedEvent>();

			// Act
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events);

			// Assert
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "1"))
				.SequenceEqual(events));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(Array.Empty<string>()));
		}

		[Fact]
		public async Task AppendEvents_One_Async()
		{
			// Arrange
			var events = new[]
			{
				new WrappedEvent(1),
			};

			// Act
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events);

			// Assert
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "1"))
				.SequenceEqual(events));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "1" }));
		}

		[Fact]
		public async Task AppendEvents_Two_Async()
		{
			// Arrange
			var events = new[]
			{
				new WrappedEvent(1),
				new WrappedEvent(2),
			};

			// Act
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events);

			// Assert
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "1"))
				.SequenceEqual(events));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "1" }));
		}

		[Fact]
		public async Task AppendEvents_NegativeSequenceId_Async()
		{
			// Arrange
			var events = new[]
			{
				new WrappedEvent(-1)
			};

			// Act
			async Task Action()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events!);
			}

			// Assert
			(await Assert.ThrowsAsync<ArgumentException>(Action))
				.Message.Contains("first event sequenceId is not natural number");
		}

		[Fact]
		public async Task AppendEvents_SkippedSequenceId_Async()
		{
			// Arrange
			var events = new[]
			{
				new WrappedEvent(2)
			};

			// Act
			async Task Action()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events!);
			}

			// Assert
			(await Assert.ThrowsAsync<ArgumentException>(Action))
				.Message.Contains("invalid firstSequenceId (gap in sequence ids) expected: '1' given: '2'");
		}

		[Fact]
		public async Task AppendEvents_OptimisticConcurrency_Async()
		{
			// Arrange
			var events = new[]
			{
				new WrappedEvent(1)
			};
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events);

			// Act
			async Task Action()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events!);
			}

			// Assert
			(await Assert.ThrowsAsync<OptimisticConcurrencyException>(Action))
				.Message.Contains("Conflict");
		}

		[Fact]
		public async Task AppendEventsAsync_Interleaving_Async()
		{
			// Arrange
			var events_a_0 = new[] { new WrappedEvent(1) };
			var events_b_0 = new[] { new WrappedEvent(1) };
			var events_a_1 = new[] { new WrappedEvent(2) };
			var events_b_1 = new[] { new WrappedEvent(2) };

			// Act
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "a", events_a_0);
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "b", events_b_0);
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "a", events_a_1);
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "b", events_b_1);

			// Assert
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "a"))
				.SequenceEqual(events_a_0.Concat(events_a_1)));
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "b"))
				.SequenceEqual(events_b_0.Concat(events_b_1)));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "a", "b" }));
		}

		[Fact]
		public async Task AppendEventsAsync_InterleavingConflict_Async()
		{
			// Arrange
			var events_a_0 = new[] { new WrappedEvent(1) };
			var events_b_0 = new[] { new WrappedEvent(1) };
			var events_a_1 = new[] { new WrappedEvent(2) };

			// Act
			async Task Action()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "a", events_a_0);
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "b", events_b_0);
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "a", events_a_1);
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "a", events_a_1);
			}

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(Action);
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "a"))
				.SequenceEqual(events_a_0.Concat(events_a_1)));
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "b"))
				.SequenceEqual(events_b_0));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "a", "b" }));
		}

		[Fact]
		public async Task AppendEvents_AppendIsAtomic_Async()
		{
			// Arrange
			var events_1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
			var events_2 = new[] { new TestWrappedEvent(2, "b"), new TestWrappedEvent(3, "b") };

			// Act
			async Task Action()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events_1!);
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events_2!);
			}

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(Action);
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "1"))
				.Cast<TestWrappedEvent>().SequenceEqual(events_1));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "1" }));
		}

		[Theory]
		[InlineData(nameof(TestInMemoryEventRepository.LockedSemaphore))]
		[InlineData(nameof(TestInMemoryEventRepository.AppendedSemaphore))]
		[InlineData(nameof(TestInMemoryEventRepository.UnlockedSemaphore))]
		public async Task AppendEvents_CriticalSectionConflicts_Async(string conflictAfter)

		{
			// Arrange
			var events_1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
			var events_2 = new[] { new TestWrappedEvent(2, "b"), new TestWrappedEvent(3, "b") };

			// Act
			async Task Append1()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events_1!);
			}
			async Task Append2()
			{
				switch (conflictAfter)
				{
					case nameof(TestInMemoryEventRepository.LockedSemaphore):
						Assert.True(TestEventRepository.LockedSemaphore.Wait(_semaphoreWaitTimeout));
						break;

					case nameof(TestInMemoryEventRepository.AppendedSemaphore):
						Assert.True(TestEventRepository.AppendedSemaphore.Wait(_semaphoreWaitTimeout));
						break;

					case nameof(TestInMemoryEventRepository.UnlockedSemaphore):
						Assert.True(TestEventRepository.UnlockedSemaphore.Wait(_semaphoreWaitTimeout));
						break;

					default:
						throw new ApplicationException($"unexpected value conflictAfter: '{conflictAfter}'");
				}
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events_2!);
			}
			async Task AppendInParallel()
			{
				var task1 = Task.Run(Append1);
				var task2 = Task.Run(Append2);
				await Task.WhenAll(task1, task2);
			}
			void WaitForConflict()
			{
				Assert.True(TestEventRepository.ConflictedSemaphore.Wait(_semaphoreWaitTimeout));
			}
			switch (conflictAfter)
			{
				case nameof(TestInMemoryEventRepository.LockedSemaphore):
					TestEventRepository.LockedCallback = WaitForConflict;
					break;

				case nameof(TestInMemoryEventRepository.AppendedSemaphore):
					TestEventRepository.AppendedCallback = WaitForConflict;
					break;

				case nameof(TestInMemoryEventRepository.UnlockedSemaphore):
					TestEventRepository.UnlockedCallback = WaitForConflict;
					break;

				default:
					throw new ApplicationException($"unexpected value conflictAfter: '{conflictAfter}'");
			}

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(AppendInParallel);
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "1"))
						.Cast<TestWrappedEvent>().SequenceEqual(events_1));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "1" }));
		}

		[Theory]
		[InlineData(nameof(TestInMemoryEventRepository.LockedSemaphore))]
		[InlineData(nameof(TestInMemoryEventRepository.AppendedSemaphore))]
		[InlineData(nameof(TestInMemoryEventRepository.UnlockedSemaphore))]
		public async Task AppendEvents_CriticalAppendConflicts_Async(string conflictAfter)
		{
			// Arrange
			var events_1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
			var events_2 = new[] { new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b") };

			// Act
			async Task Append1()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events_1!);
			}
			async Task Append2()
			{
				switch (conflictAfter)
				{
					case nameof(TestInMemoryEventRepository.LockedSemaphore):
						Assert.True(TestEventRepository.LockedSemaphore.Wait(_semaphoreWaitTimeout));
						break;

					case nameof(TestInMemoryEventRepository.AppendedSemaphore):
						Assert.True(TestEventRepository.AppendedSemaphore.Wait(_semaphoreWaitTimeout));
						break;

					case nameof(TestInMemoryEventRepository.UnlockedSemaphore):
						Assert.True(TestEventRepository.UnlockedSemaphore.Wait(_semaphoreWaitTimeout));
						break;

					default:
						throw new ApplicationException($"unexpected value conflictAfter: '{conflictAfter}'");
				}
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events_2!);
			}
			async Task AppendInParallel()
			{
				var task1 = Task.Run(Append1);
				var task2 = Task.Run(Append2);
				await Task.WhenAll(task1, task2);
			}
			void WaitForConflict()
			{
				Assert.True(TestEventRepository.ConflictedSemaphore.Wait(_semaphoreWaitTimeout));
			}
			switch (conflictAfter)
			{
				case nameof(TestInMemoryEventRepository.LockedSemaphore):
					TestEventRepository.LockedCallback = WaitForConflict;
					break;

				case nameof(TestInMemoryEventRepository.AppendedSemaphore):
					TestEventRepository.AppendedCallback = WaitForConflict;
					break;

				case nameof(TestInMemoryEventRepository.UnlockedSemaphore):
					// do nothing
					break;

				default:
					throw new ApplicationException($"unexpected value conflictAfter: '{conflictAfter}'");
			}

			// Assert
			if (conflictAfter == nameof(TestInMemoryEventRepository.UnlockedSemaphore))
			{ // no conflict
				await AppendInParallel();
			}
			else
			{ // conflict
				await Assert.ThrowsAsync<OptimisticConcurrencyException>(AppendInParallel);
				// retry after optimistic concurrency conflict
				TestEventRepository.LockedCallback = null;
				TestEventRepository.AppendedCallback = null;
				TestEventRepository.UnlockedCallback = null;
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events_2!);
			}
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "1"))
						.Cast<TestWrappedEvent>().SequenceEqual(events_1.Concat(events_2)));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "1" }));
		}

		[Theory]
		[InlineData(nameof(TestInMemoryEventRepository.ValidatedCallback))]
		[InlineData(nameof(TestInMemoryEventRepository.LockedCallback))]
		[InlineData(nameof(TestInMemoryEventRepository.AppendedCallback))]
		[InlineData(nameof(TestInMemoryEventRepository.UnlockedCallback))]
		public async Task ListEventsAsync_ConflictWithAppending_Async(string listOnCallback)
		{
			// Arrange
			var events_1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
			var events_2 = new[] { new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b") };
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events_1);

			// Act
			IReadOnlyList<WrappedEvent> result = Array.Empty<WrappedEvent>().ToList().AsReadOnly();
			void ListCallback()
			{
				result = EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "1").Result;
			}
			switch (listOnCallback)
			{
				case nameof(TestInMemoryEventRepository.ValidatedCallback):
					TestEventRepository.ValidatedCallback = ListCallback;
					break;

				case nameof(TestInMemoryEventRepository.LockedCallback):
					TestEventRepository.LockedCallback = ListCallback;
					break;

				case nameof(TestInMemoryEventRepository.AppendedCallback):
					TestEventRepository.AppendedCallback = ListCallback;
					break;

				case nameof(TestInMemoryEventRepository.UnlockedCallback):
					TestEventRepository.UnlockedCallback = ListCallback;
					break;

				default:
					throw new ApplicationException($"unexpected value listOnCallback: '{listOnCallback}'");
			}
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events_2);

			// Assert
			var expected = events_1.AsEnumerable();
			switch (listOnCallback)
			{
				case nameof(TestInMemoryEventRepository.AppendedCallback):
				case nameof(TestInMemoryEventRepository.UnlockedCallback):
					expected = expected.Concat(events_2);
					break;
			}
			Assert.True(result.SequenceEqual(expected));
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
			// Arrange
			var events = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a"),
				new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b") };
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events);

			// Act
			var result = await EventRepository.ListEventsAsync(
				nameof(TestRoundAggregate), "1", afterSequenceId, limit);

			// Assert
			Assert.True(result.Count <= limit);
			Assert.True(result.All(a => afterSequenceId < a.SequenceId));
		}

		[Fact]
		public async Task ListAggregateIdsAsync_Async()
		{
			// Arrange
			var events = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a"),
				new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b") };
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "1", events);
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "2", events);

			// Act
			var result = await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate));

			// Assert
			Assert.True(result.SequenceEqual(new[] { "1", "2" }));
		}
	}
}
