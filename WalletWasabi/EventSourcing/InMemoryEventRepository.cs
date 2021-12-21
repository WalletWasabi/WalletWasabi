using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Exceptions;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Helpers;

namespace WalletWasabi.EventSourcing
{
	/// <summary>
	/// Thread safe without locks in memory event repository implementation
	/// </summary>
	public class InMemoryEventRepository : IEventRepository
	{
		private static readonly IReadOnlyList<WrappedEvent> EmptyResult
			= ImmutableList<WrappedEvent>.Empty;

		private static readonly IReadOnlyList<string> EmptyIds
			= ImmutableList<string>.Empty;

		private static readonly IComparer<WrappedEvent> WrappedEventSequenceIdComparer
			= Comparer<WrappedEvent>.Create((a, b) => a.SequenceId.CompareTo(b.SequenceId));

		private ConcurrentDictionary
			// aggregateType
			<string,
			ConcurrentDictionary
				// aggregateId
				<string, AggregateEvents>> AggregatesEventsBatches
		{ get; } = new();

		private ConcurrentDictionary
			// aggregateType
			<string,
			AggregateTypeIds> AggregatesIds
		{ get; } = new();

		/// <inheritdoc/>
		public Task AppendEventsAsync(
			string aggregateType,
			string aggregateId,
			IEnumerable<WrappedEvent> wrappedEvents)
		{
			Guard.NotNullOrEmpty(nameof(aggregateType), aggregateType);
			Guard.NotNullOrEmpty(nameof(aggregateId), aggregateId);
			Guard.NotNull(nameof(wrappedEvents), wrappedEvents);

			var wrappedEventsList = wrappedEvents.ToList().AsReadOnly();

			if (wrappedEventsList.Count == 0)
			{
				return Task.CompletedTask;
			}
			var firstSequenceId = wrappedEventsList[0].SequenceId;
			var lastSequenceId = wrappedEventsList[^1].SequenceId;

			if (firstSequenceId <= 0)
			{
				throw new ArgumentException("First event sequenceId is not natural number.", nameof(wrappedEvents));
			}

			if (lastSequenceId <= 0)
			{
				throw new ArgumentException("Last event sequenceId is not a positive integer.", nameof(wrappedEvents));
			}

			if (lastSequenceId - firstSequenceId + 1 != wrappedEventsList.Count)
			{
				throw new ArgumentException("Event sequence ids are inconsistent.", nameof(wrappedEvents));
			}

			var aggregateEventsBatches = AggregatesEventsBatches.GetOrAdd(aggregateType, _ => new());
			var (tailSequenceId, events) = aggregateEventsBatches.GetOrAdd(
				aggregateId,
				_ => new AggregateEvents(0, ImmutableList<WrappedEvent>.Empty));

			if (tailSequenceId + 1 < firstSequenceId)
			{
				throw new ArgumentException($"Invalid firstSequenceId (gap in sequence ids) expected: '{tailSequenceId + 1}' given: '{firstSequenceId}'.", nameof(wrappedEvents));
			}

			// no action
			Validated();

			var newEvents = events.AddRange(wrappedEventsList);

			// Atomically detect conflict and replace lastSequenceId and lock to ensure strong order in eventsBatches.
			if (!aggregateEventsBatches.TryUpdate(
				key: aggregateId,
				newValue: new AggregateEvents(lastSequenceId, newEvents),
				comparisonValue: new AggregateEvents(firstSequenceId - 1, events)))
			{
				Conflicted(); // no action
				throw new OptimisticConcurrencyException(
					$"Conflict while committing events. Retry command. aggregate: '{aggregateType}' id: '{aggregateId}'");
			}
			Appended(); // no action

			// If it is a first event for given aggregate.
			if (tailSequenceId == 0)
			{
				// Add index of aggregate id into the dictionary.
				IndexNewAggregateId(aggregateType, aggregateId);
			}
			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public Task<IReadOnlyList<WrappedEvent>> ListEventsAsync(
			string aggregateType,
			string aggregateId,
			long afterSequenceId = 0,
			int? maxCount = null)
		{
			Guard.NotNull(nameof(aggregateType), aggregateType);
			Guard.NotNull(nameof(aggregateId), aggregateId);
			if (AggregatesEventsBatches.TryGetValue(aggregateType, out var aggregateEventsBatches) &&
				aggregateEventsBatches.TryGetValue(aggregateId, out var value))
			{
				var result = value.Events;

				if (afterSequenceId > 0)
				{
					var dummyEvent = new WrappedEvent(afterSequenceId, null!, Guid.Empty);
					var foundIndex = result.BinarySearch(dummyEvent, WrappedEventSequenceIdComparer);
					if (foundIndex < 0)
					{
						// Note: this is because of BinarySearch() documented implementation
						// returns "bitwise complement"
						// see: https://docs.microsoft.com/en-us/dotnet/api/system.collections.immutable.immutablelist-1.binarysearch
						// The zero-based index of item in the sorted List, if item is found;
						// otherwise, a negative number that is the bitwise complement
						// of the index of the next element that is larger than item or,
						// if there is no larger element, the bitwise complement of Count.
						foundIndex = ~foundIndex;
					}
					else
					{
						foundIndex++;
					}
					result = result.GetRange(foundIndex, result.Count - foundIndex);
				}

				if (maxCount < result.Count)
				{
					result = result.GetRange(0, maxCount.Value);
				}

				return Task.FromResult((IReadOnlyList<WrappedEvent>)result);
			}
			return Task.FromResult(EmptyResult);
		}

		/// <inheritdoc/>
		public Task<IReadOnlyList<string>> ListAggregateIdsAsync(
			string aggregateType,
			string? afterAggregateId = null,
			int? maxCount = null)
		{
			if (AggregatesIds.TryGetValue(aggregateType, out var aggregateIds))
			{
				var ids = aggregateIds.Ids;
				var foundIndex = 0;
				if (afterAggregateId != null)
				{
					foundIndex = ids.IndexOf(afterAggregateId);
					if (foundIndex < 0)
					{
						foundIndex = ~foundIndex;
					}
					else
					{
						foundIndex++;
					}
				}
				List<string> result = new();
				var afterLastIndex = maxCount.HasValue
					? Math.Min(foundIndex + maxCount.Value, ids.Count)
					: ids.Count;
				for (var i = foundIndex; i < afterLastIndex; i++)
				{
					result.Add(ids[i]);
				}
				return Task.FromResult((IReadOnlyList<string>)result.AsReadOnly());
			}
			return Task.FromResult(EmptyIds);
		}

		private void IndexNewAggregateId(string aggregateType, string aggregateId)
		{
			var tailIndex = 0L;
			ImmutableSortedSet<string> aggregateIds;
			ImmutableSortedSet<string> newAggregateIds;
			var liveLockLimit = 10000;
			do
			{
				if (liveLockLimit <= 0)
				{
					throw new ApplicationException("Live lock detected.");
				}
				liveLockLimit--;
				(tailIndex, aggregateIds) = AggregatesIds.GetOrAdd(aggregateType, _ => new(0, ImmutableSortedSet<string>.Empty));
				newAggregateIds = aggregateIds.Add(aggregateId);
			}
			while (!AggregatesIds.TryUpdate(
				key: aggregateType,
				newValue: new AggregateTypeIds(tailIndex + 1, newAggregateIds),
				comparisonValue: new AggregateTypeIds(tailIndex, aggregateIds)));
		}

		// Hook for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Validated()
		{
			// Keep empty. To be overriden in tests.
		}

		// Hook for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Conflicted()
		{
			// Keep empty. To be overriden in tests.
		}

		// Hook for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Appended()
		{
			// Keep empty. To be overriden in tests.
		}
	}
}
