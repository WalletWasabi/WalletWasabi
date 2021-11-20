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

namespace WalletWasabi.Tests.UnitTests.EventSourcing
{
	public class InMemoryEventRepositoryTests
	{
		private const string TestRoundAggregate = "TestRoundAggregate";

		private IEventRepository EventRepository { get; } = new InMemoryEventRepository();

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

		[Fact]
		public void ConcurrentStackOrQueue()
		{
			var stack = new ConcurrentStack<int>();
			var queue = new ConcurrentQueue<int>();
			stack.Push(1);
			stack.Push(2);
			queue.Enqueue(1);
			queue.Enqueue(2);
			var stackList = stack.ToList();
			var queueList = queue.ToList();
			Assert.True(queueList.SequenceEqual(new[] { 1, 2 }));
			Assert.True(stackList.SequenceEqual(new[] { 2, 1 }));
		}
	}
}
