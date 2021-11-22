using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces.EventSourcing;

namespace WalletWasabi.EventSourcing
{
	/// <summary>
	/// Thread safe without locks in memory event repository implementation
	/// </summary>
	public class InMemoryEventRepository : IEventRepository
	{
		protected static readonly IReadOnlyList<WrappedEvent> EmptyResult = Array.Empty<WrappedEvent>().ToList().AsReadOnly();
		protected static readonly IReadOnlyList<string> EmptyIds = Array.Empty<string>().ToList().AsReadOnly();

		private readonly ConcurrentDictionary<
			string /*aggregateType*/,
			ConcurrentDictionary<
				string /*aggregateId*/,
				(
					// SequenceId of the last event of this aggregate
					long TailSequenceId,

					// locked flag for appending into EventsBatches of this aggregate
					bool Locked,

					// list of lists of events for atomic insertion of multiple events
					// in one "database transaction"
					ConcurrentQueue<IReadOnlyList<WrappedEvent>> EventsBatches
				)
			>
		> _aggregatesEventsBatches = new();

		private readonly ConcurrentDictionary<
			string /*aggregateType*/,
			(
				// index of the last aggregateId in this aggregateType
				long TailIndex,
				ConcurrentDictionary<
					// index of aggregateId in this aggregateType
					long,
					string /*aggregateId*/> Ids
			)
		> _aggregatesIds = new();

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
				throw new ArgumentException("first event sequenceId is not natural number", nameof(wrappedEvents));
			}
			if (lastSequenceId <= 0)
			{
				throw new ArgumentException("last event sequenceId is not natural number", nameof(wrappedEvents));
			}
			if (lastSequenceId - firstSequenceId + 1 != wrappedEventsList.Count)
			{
				throw new ArgumentException("event sequence ids are out of whack", nameof(wrappedEvents));
			}

			var aggregateEventsBatches = _aggregatesEventsBatches.GetOrAdd(aggregateType, _ => new());
			var (tailSequenceId, locked, eventsBatches) = aggregateEventsBatches.GetOrAdd(aggregateId, _ => (0, false, new()));

			if (tailSequenceId + 1 < firstSequenceId)
			{
				throw new ArgumentException($"invalid firstSequenceId (gap in sequence ids) expected: '{tailSequenceId + 1}' given: '{firstSequenceId}'", nameof(wrappedEvents));
			}

			// no action
			Validated();
			// atomically detect conflict and replace lastSequenceId and lock to ensure strong order in eventsBatches
			if (!aggregateEventsBatches.TryUpdate(
				key: aggregateId,
				newValue: (lastSequenceId, true, eventsBatches),
				comparisonValue: (firstSequenceId - 1, false, eventsBatches)))
			{
				Conflicted(); // no action
				throw new OptimisticConcurrencyException($"Conflict while commiting events. Retry command. aggregate: '{aggregateType}' id: '{aggregateId}'");
			}
			try
			{
				Locked(); // no action
				eventsBatches.Enqueue(wrappedEventsList);
				Appended(); // no action
			}
			finally
			{
				// unlock
				if (!aggregateEventsBatches.TryUpdate(
					key: aggregateId,
					newValue: (lastSequenceId, false, eventsBatches),
					comparisonValue: (lastSequenceId, true, eventsBatches)))
				{
#warning
					// TODO: convert into Debug.Assert ???
					throw new ApplicationException("unexpected failure 89#");
				}
				Unlocked(); // no action
			}

			// if it is a first event for given aggregate
			if (tailSequenceId == 0)
			{ // add index of aggregate id into the dictionary
				IndexNewAggregateId(aggregateType, aggregateId);
			}
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<WrappedEvent>> ListEventsAsync(string aggregateType,
			string aggregateId, long afterSequenceId = -1, int? limit = null)
		{
			if (_aggregatesEventsBatches.TryGetValue(aggregateType, out var aggregateEventsBatches) &&
				aggregateEventsBatches.TryGetValue(aggregateId, out var value))
			{
				var result = value.EventsBatches.SelectMany(a => a);
				if (-1 < afterSequenceId)
				{
					result = result.Where(a => afterSequenceId < a.SequenceId);
				}
				if (limit.HasValue)
				{
					result = result.Take(limit.Value);
				}
				return Task.FromResult((IReadOnlyList<WrappedEvent>)result.ToList().AsReadOnly());
			}
			return Task.FromResult(EmptyResult);
		}

		public Task<IReadOnlyList<string>> ListAggregateIdsAsync(string aggregateType,
			string? afterId = null, int? limit = null)
		{
			if (afterId != null)
			{
				throw new NotImplementedException();
			}
			if (limit != null)
			{
				throw new NotImplementedException();
			}
			if (_aggregatesIds.TryGetValue(aggregateType, out var tuple))
			{
				var tailIndex = tuple.TailIndex;
				var ids = tuple.Ids;
				var result = new List<string>();
				for (var i = 1L; i <= tailIndex; i++)
				{
					if (!ids.TryGetValue(i, out var id))
					{
#warning
						// TODO: convert into Debug.Assert ???
						throw new ApplicationException("unexpected failure #123");
					}
					result.Add(id);
				}
				return Task.FromResult((IReadOnlyList<string>)result.AsReadOnly());
			}
			return Task.FromResult(EmptyIds);
		}

		private void IndexNewAggregateId(string aggregateType, string id)
		{
			var tailIndex = 0L;
			ConcurrentDictionary<long, string> aggregateIds;
			var liveLockLimit = 10000;
			do
			{
				if (liveLockLimit-- <= 0)
				{
					throw new ApplicationException("live lock detected");
				}
				(tailIndex, aggregateIds) = _aggregatesIds.GetOrAdd(aggregateType,
					_ => new(0, new()));
			}
			while (!_aggregatesIds.TryUpdate(
				key: aggregateType,
				newValue: (tailIndex + 1, aggregateIds),
				comparisonValue: (tailIndex, aggregateIds)));
			if (!aggregateIds.TryAdd(tailIndex + 1, id))
			{
#warning
				// TODO: convert into Debug.Assert ???
				throw new ApplicationException("unexpected failure #167");
			}
		}

		// helper for parallel critical section testing in DEBUG build only
		[Conditional("DEBUG")]
		protected virtual void Validated()
		{
		}

		// helper for parallel critical section testing in DEBUG build only
		[Conditional("DEBUG")]
		protected virtual void Conflicted()
		{
		}

		// helper for parallel critical section testing in DEBUG build only
		[Conditional("DEBUG")]
		protected virtual void Locked()
		{
		}

		// helper for parallel critical section testing in DEBUG build only
		[Conditional("DEBUG")]
		protected virtual void Appended()
		{
		}

		// helper for parallel critical section testing in DEBUG build only
		[Conditional("DEBUG")]
		protected virtual void Unlocked()
		{
		}
	}
}
