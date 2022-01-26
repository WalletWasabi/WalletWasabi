using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.Interfaces.EventSourcing;

namespace WalletWasabi.EventSourcing;

/// <summary>
/// Thread safe without locks in memory event repository implementation
/// </summary>
public class InMemoryEventRepository : IEventRepository
{
	private static readonly IReadOnlyList<WrappedEvent> EmptyResult = ImmutableList<WrappedEvent>.Empty;

	private static readonly IReadOnlyList<string> EmptyIds = ImmutableList<string>.Empty;

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
	public async Task AppendEventsAsync(string aggregateType, string aggregateId, IEnumerable<WrappedEvent> wrappedEvents)
	{
		ReadOnlyCollection<WrappedEvent> wrappedEventsList = wrappedEvents.ToList().AsReadOnly();

		if (wrappedEventsList.Count == 0)
		{
			return;
		}

		long firstSequenceId = wrappedEventsList[0].SequenceId;
		long lastSequenceId = wrappedEventsList[^1].SequenceId;

		if (firstSequenceId <= 0)
		{
			throw new ArgumentException("First event sequenceId is not a positive number.", nameof(wrappedEvents));
		}

		if (lastSequenceId <= 0)
		{
			throw new ArgumentException("Last event sequenceId is not a positive integer.", nameof(wrappedEvents));
		}

		if (lastSequenceId - firstSequenceId + 1 != wrappedEventsList.Count)
		{
			throw new ArgumentException("Event sequence IDs are inconsistent.", nameof(wrappedEvents));
		}

		var aggregateEventsBatches = AggregatesEventsBatches.GetOrAdd(aggregateType, _ => new());
		var (tailSequenceId, events) = aggregateEventsBatches.GetOrAdd(
			aggregateId,
			_ => new AggregateEvents(0, ImmutableList<WrappedEvent>.Empty));

		if (tailSequenceId + 1 < firstSequenceId)
		{
			throw new ArgumentException($"Invalid firstSequenceId (gap in sequence IDs) expected: '{tailSequenceId + 1}' given: '{firstSequenceId}'.", nameof(wrappedEvents));
		}

		// No action.
		await ValidatedAsync().ConfigureAwait(false);

		ImmutableList<WrappedEvent> newEvents = events.AddRange(wrappedEventsList);

		// Atomically detect conflict and replace lastSequenceId and lock to ensure strong order in eventsBatches.
		if (!aggregateEventsBatches.TryUpdate(
			key: aggregateId,
			newValue: new AggregateEvents(lastSequenceId, newEvents),
			comparisonValue: new AggregateEvents(firstSequenceId - 1, events)))
		{
			await ConflictedAsync().ConfigureAwait(false);

			throw new OptimisticConcurrencyException($"Conflict while committing events. Retry command. aggregate: '{aggregateType}' ID: '{aggregateId}'");
		}

		await AppendedAsync().ConfigureAwait(false);

		// If it is a first event for given aggregate.
		if (tailSequenceId == 0)
		{
			// Add index of aggregate id into the dictionary.
			IndexNewAggregateId(aggregateType, aggregateId);
		}
	}

	/// <remarks>
	/// Working with <see cref="ImmutableList{T}.BinarySearch(int, int, T, IComparer{T}?)"/> is explained here
	/// https://docs.microsoft.com/en-us/dotnet/api/system.collections.immutable.immutablelist-1.binarysearch.
	/// </remarks>
	/// <inheritdoc/>
	public Task<IReadOnlyList<WrappedEvent>> GetEventsAsync(string aggregateType, string aggregateId, long afterSequenceId = 0, int? maxCount = null)
	{
		if (AggregatesEventsBatches.TryGetValue(aggregateType, out ConcurrentDictionary<string, AggregateEvents>? aggregateEventsBatches) &&
			aggregateEventsBatches.TryGetValue(aggregateId, out AggregateEvents? value))
		{
			ImmutableList<WrappedEvent> result = value.Events;

			if (afterSequenceId > 0)
			{
				int foundIndex = result.BinarySearch(new WrappedEvent(afterSequenceId), WrappedEventSequenceIdComparer);

				if (foundIndex < 0)
				{
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
	public Task<IReadOnlyList<string>> GetAggregateIdsAsync(string aggregateType, string? afterAggregateId = null, int? maxCount = null)
	{
		if (AggregatesIds.TryGetValue(aggregateType, out var aggregateIds))
		{
			ImmutableSortedSet<string> ids = aggregateIds.Ids;
			int foundIndex = 0;

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
			int afterLastIndex = maxCount.HasValue
				? Math.Min(foundIndex + maxCount.Value, ids.Count)
				: ids.Count;

			for (int i = foundIndex; i < afterLastIndex; i++)
			{
				result.Add(ids[i]);
			}

			return Task.FromResult((IReadOnlyList<string>)result.AsReadOnly());
		}

		return Task.FromResult(EmptyIds);
	}

	private void IndexNewAggregateId(string aggregateType, string aggregateId)
	{
		ImmutableSortedSet<string> aggregateIds;
		ImmutableSortedSet<string> newAggregateIds;

		long tailIndex = 0L;
		int liveLockLimit = 10000;

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

	/// <summary>Helper method for verifying invariants in tests.</summary>
	/// <remarks>Do not add any code to this method.</remarks>
	protected virtual Task ValidatedAsync()
	{
		return Task.CompletedTask;
	}

	/// <summary>Helper method for verifying invariants in tests.</summary>
	/// <remarks>Do not add any code to this method.</remarks>
	protected virtual Task ConflictedAsync()
	{
		return Task.CompletedTask;
	}

	/// <summary>Helper method for verifying invariants in tests.</summary>
	/// <remarks>Do not add any code to this method.</remarks>
	protected virtual Task AppendedAsync()
	{
		return Task.CompletedTask;
	}
}
