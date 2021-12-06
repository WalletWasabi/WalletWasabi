using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces.EventSourcing;

namespace WalletWasabi.EventSourcing
{
	/// <summary>
	/// Thread safe without locks in memory event repository implementation
	/// </summary>
	public class InMemoryEventRepository : IEventRepository
	{
		protected static readonly IReadOnlyList<WrappedEvent> EmptyResult
			= ImmutableList<WrappedEvent>.Empty;

		protected static readonly IReadOnlyList<string> EmptyIds
			= ImmutableList<string>.Empty;

		protected static readonly IComparer<WrappedEvent> WrappedEventSequenceIdComparer
			= Comparer<WrappedEvent>.Create((a, b) => a.SequenceId.CompareTo(b.SequenceId));

		private ConcurrentDictionary
			// aggregateType
			<string,
			ConcurrentDictionary
				// aggregateId
				<string,
				(
					// SequenceId of the last event of this aggregate
					long TailSequenceId,

					// Ordered list of events
					ImmutableList<WrappedEvent> Events
				)>> AggregatesEventsBatches
		{ get; } = new();

		private ConcurrentDictionary
			// aggregateType
			<string,
			(
				// Index of the last aggregateId in this aggregateType
				long TailIndex,

				// List of aggregate Ids in this aggregateType
				ImmutableSortedSet<string> Ids
			)> AggregatesIds
		{ get; } = new();

		public Task AppendEventsAsync(
			string aggregateType,
			string aggregateId,
			IEnumerable<WrappedEvent> wrappedEvents)
		{
			Guard.NotNullOrEmpty(nameof(aggregateType), aggregateType);
			Guard.NotNullOrEmpty(nameof(aggregateId), aggregateId);
			Guard.NotNull(nameof(wrappedEvents), wrappedEvents);
			var wrappedEventsList = wrappedEvents.ToList().AsReadOnly();
			if (wrappedEventsList.Count <= 0)
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
				throw new ArgumentException("Last event sequenceId is not natural number.", nameof(wrappedEvents));
			}
			if (lastSequenceId - firstSequenceId + 1 != wrappedEventsList.Count)
			{
				throw new ArgumentException("Event sequence ids are inconsistent.", nameof(wrappedEvents));
			}

			var aggregateEventsBatches = AggregatesEventsBatches.GetOrAdd(aggregateType, _ => new());
			var (tailSequenceId, events) = aggregateEventsBatches.GetOrAdd(
				aggregateId,
				_ => (0, ImmutableList<WrappedEvent>.Empty));

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
				newValue: (lastSequenceId, newEvents),
				comparisonValue: (firstSequenceId - 1, events)))
			{
				Conflicted(); // no action
				throw new OptimisticConcurrencyException($"Conflict while commiting events. Retry command. aggregate: '{aggregateType}' id: '{aggregateId}'");
			}
			Appended(); // no action

			// If it is a first event for given aggregate.
			if (tailSequenceId == 0)
			{ // Add index of aggregate id into the dictionary.
				IndexNewAggregateId(aggregateType, aggregateId);
			}
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<WrappedEvent>> ListEventsAsync(
			string aggregateType,
			string aggregateId,
			long afterSequenceId = 0,
			int? maxCount = null)
		{
			if (AggregatesEventsBatches.TryGetValue(aggregateType, out var aggregateEventsBatches) &&
				aggregateEventsBatches.TryGetValue(aggregateId, out var value))
			{
				var result = value.Events;
				if (afterSequenceId > 0)
				{
					var foundIndex = result.BinarySearch(
						new WrappedEvent(afterSequenceId),
						WrappedEventSequenceIdComparer);
					if (foundIndex < 0)
					{
						foundIndex = ~foundIndex;
					}
					else
					{
						foundIndex++;
					}
					result = result.RemoveRange(0, foundIndex);
				}
				if (maxCount.HasValue && maxCount.Value < result.Count)
				{
					result = result.RemoveRange(maxCount.Value, result.Count - maxCount.Value);
				}
				return Task.FromResult((IReadOnlyList<WrappedEvent>)result);
			}
			return Task.FromResult(EmptyResult);
		}

		public Task<IReadOnlyList<string>> ListAggregateIdsAsync(
			string aggregateType,
			string? afterAggregateId = null,
			int? maxCount = null)
		{
			if (AggregatesIds.TryGetValue(aggregateType, out var tuple))
			{
				var ids = tuple.Ids;
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
				var result = new List<string>();
				var afterLastIndex = maxCount.HasValue ?
					Math.Min(foundIndex + maxCount.Value, ids.Count) :
					ids.Count;
				for (var i = foundIndex; i < afterLastIndex; i++)
				{
					result.Add(ids[i]);
				}
				return Task.FromResult((IReadOnlyList<string>)result.AsReadOnly());
			}
			return Task.FromResult(EmptyIds);
		}

		private void IndexNewAggregateId(string aggregateType, string id)
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
				newAggregateIds = aggregateIds.Add(id);
			}
			while (!AggregatesIds.TryUpdate(
				key: aggregateType,
				newValue: (tailIndex + 1, newAggregateIds),
				comparisonValue: (tailIndex, aggregateIds)));
		}

		// Helper for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Validated()
		{
		}

		// Helper for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Conflicted()
		{
		}

		// helper for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Appended()
		{
		}
	}
}
