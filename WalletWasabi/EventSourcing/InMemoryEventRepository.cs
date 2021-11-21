using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
			ConcurrentDictionary<string /*id*/,
				(long LastSequenceId,
				bool Locked,
					ConcurrentQueue<IReadOnlyList<WrappedEvent>> EventsBatches) /* sequence of atomically appended event batches */>>
						_aggregatesEventsBatches = new();

		private readonly ConcurrentDictionary<
			string /*aggregateType*/,
			(long LastIndex,
			ConcurrentDictionary<
				long, /*index*/
				string /*id*/> Ids)>
					_aggregatesIds = new();

		public Task AppendEventsAsync(
			string aggregateType,
			string id,
			IEnumerable<WrappedEvent> wrappedEvents)
		{
			Guard.NotNullOrEmpty(nameof(aggregateType), aggregateType);
			Guard.NotNullOrEmpty(nameof(id), id);
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
			var (concurrentSequenceId, locked, eventsBatches) = aggregateEventsBatches.GetOrAdd(id, _ => (0, false, new()));

			if (concurrentSequenceId + 1 < firstSequenceId)
			{
				throw new ArgumentException($"invalid firstSequenceId (gap in sequence ids) expected: '{concurrentSequenceId + 1}' given: '{firstSequenceId}'", nameof(wrappedEvents));
			}
			// atomically detect conflict and replace lastSequenceId and lock to ensure strong order in eventsBatches
			if (!aggregateEventsBatches.TryUpdate(id, (lastSequenceId, true, eventsBatches), (firstSequenceId - 1, false, eventsBatches)))
			{
				throw new OptimisticConcurrencyException($"Conflict while commiting events. Retry command. aggregate: '{aggregateType}' id: '{id}'");
			}
			try
			{
				eventsBatches.Enqueue(wrappedEventsList);
			}
			finally
			{
				// unlock
				if (!aggregateEventsBatches.TryUpdate(id, (lastSequenceId, false, eventsBatches), (lastSequenceId, true, eventsBatches)))
				{
#warning
					// TODO: convert into Debug.Assert ???
					throw new ApplicationException("unexpected failure 89#");
				}
			}

			// if it is a first event for given aggregate
			if (concurrentSequenceId == 0)
			{ // add index of aggregate id into the dictionary
				IndexNewAggregateId(aggregateType, id);
			}
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<WrappedEvent>> ListEventsAsync(string aggregateType, string id, long fromSequenceId = 0, int? limit = null)
		{
			if (_aggregatesEventsBatches.TryGetValue(aggregateType, out var aggregateEventsBatches) &&
				aggregateEventsBatches.TryGetValue(id, out var value))
			{
				var result = value.EventsBatches.SelectMany(a => a);
				if (0 < fromSequenceId)
				{
					result = result.Where(a => fromSequenceId <= a.SequenceId);
				}
				if (limit.HasValue)
				{
					result = result.Take(limit.Value);
				}
				return Task.FromResult((IReadOnlyList<WrappedEvent>)result.ToList().AsReadOnly());
			}
			return Task.FromResult(EmptyResult);
		}

		public Task<IReadOnlyList<string>> ListAggregateIdsAsync(string aggregateType, string? fromId = null, int? limit = null)
		{
			if (fromId != null)
			{
				throw new NotImplementedException();
			}
			if (limit != null)
			{
				throw new NotImplementedException();
			}
			if (_aggregatesIds.TryGetValue(aggregateType, out var tuple))
			{
				var lastIndex = tuple.LastIndex;
				var ids = tuple.Ids;
				var result = new List<string>();
				for (var i = 1L; i <= lastIndex; i++)
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
			var lastIndex = 0L;
			ConcurrentDictionary<long, string> aggregateIds;
			var liveLockLimit = 10000;
			do
			{
				if (liveLockLimit-- <= 0)
				{
					throw new ApplicationException("live lock detected");
				}
				(lastIndex, aggregateIds) = _aggregatesIds.GetOrAdd(aggregateType, _ => new(0, new()));
			} while (!_aggregatesIds.TryUpdate(aggregateType, (lastIndex + 1, aggregateIds), (lastIndex, aggregateIds)));
			if (!aggregateIds.TryAdd(lastIndex + 1, id))
			{
#warning
				// TODO: convert into Debug.Assert ???
				throw new ApplicationException("unexpected failure #167");
			}
		}
	}
}
