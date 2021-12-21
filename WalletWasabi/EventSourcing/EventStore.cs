using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Exceptions;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.EventSourcing
{
	public class EventStore : IEventStore
	{
		public const int OptimisticRetryLimit = 10;

		private IEventRepository EventRepository { get; }

		private IAggregateFactory AggregateFactory { get; init; }
		private ICommandProcessorFactory CommandProcessorFactory { get; init; }

		public EventStore(
			IEventRepository eventRepository,
			IAggregateFactory aggregateFactory,
			ICommandProcessorFactory commandProcessorFactory)
		{
			EventRepository = eventRepository;
			AggregateFactory = aggregateFactory;
			CommandProcessorFactory = commandProcessorFactory;
		}

		/// <inheritdoc />
		public async Task<WrappedResult> ProcessCommandAsync(ICommand command, string aggregateType, string aggregateId)
		{
			Guard.NotNull(nameof(command), command);
			var tries = OptimisticRetryLimit + 1;
			var optimisticConflict = false;
			do
			{
				tries--;
				optimisticConflict = false;
				try
				{
					return await DoProcessCommandAsync(command, aggregateType, aggregateId).ConfigureAwait(false);
				}
				catch (OptimisticConcurrencyException)
				{
					// No action
					Conflicted();
					if (tries <= 0)
					{
						throw;
					}
					optimisticConflict = true;
				}
			} while (optimisticConflict && tries > 0);
			throw new AssertionFailedException($"Unexpected code reached in {nameof(ProcessCommandAsync)}");
		}

		/// <inheritdoc />
		public async Task<IAggregate> GetAggregateAsync(string aggregateType, string aggregateId)
		{
			var events = await ListEventsAsync(aggregateType, aggregateId).ConfigureAwait(false);

			return ApplyEvents(aggregateType, events);
		}

		private async Task<WrappedResult> DoProcessCommandAsync(ICommand command, string aggregateType, string aggregateId)
		{
			var events = await ListEventsAsync(aggregateType, aggregateId).ConfigureAwait(false);
			var aggregate = ApplyEvents(aggregateType, events);
			var lastEvent = events.Count > 0 ? events[^1] : null;
			var sequenceId = lastEvent == null ? 0 : lastEvent.SequenceId;

			bool commandAlreadyProcessed = events.Any(ev => ev.SourceId == command.IdempotenceId);
			if (commandAlreadyProcessed)
			{
				return new WrappedResult(
					sequenceId,
					ImmutableList<WrappedEvent>.Empty,
					aggregate.State,
					IdempotenceIdDuplicate: true);
			}

			if (!CommandProcessorFactory.TryCreate(aggregateType, out var processor))
			{
				throw new AssertionFailedException($"CommandProcessor is missing for aggregate type '{aggregateType}'.");
			}

			Result? result = null;
			try
			{
				result = processor.Process(command, aggregate.State);
				if (result.Success)
				{
					List<WrappedEvent> wrappedEvents = new();
					foreach (var newEvent in result.Events)
					{
						sequenceId++;
						wrappedEvents.Add(new WrappedEvent(sequenceId, newEvent, command.IdempotenceId));
						aggregate.Apply(newEvent);
					}

					// No ation
					Prepared();

					await EventRepository.AppendEventsAsync(aggregateType, aggregateId, wrappedEvents)
						.ConfigureAwait(false);

					// No action
					Appended();

					return new WrappedResult(sequenceId, wrappedEvents.AsReadOnly(), aggregate.State);
				}
			}
			catch (OptimisticConcurrencyException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new CommandFailedException(
					aggregateType,
					aggregateId,
					sequenceId,
					aggregate.State,
					command,
					ImmutableArray.Create(new Error(ex)),
					null,
					ex);
			}
			if (result?.Success == false)
			{
				throw new CommandFailedException(
					aggregateType,
					aggregateId,
					sequenceId,
					aggregate.State,
					command,
					result.Errors);
			}
			throw new AssertionFailedException($"Unexpected reached in {nameof(EventStore)}.{nameof(DoProcessCommandAsync)}().");
		}

		private IAggregate ApplyEvents(string aggregateType, IReadOnlyList<WrappedEvent> events)
		{
			if (!AggregateFactory.TryCreate(aggregateType, out var aggregate))
			{
				throw new InvalidOperationException($"AggregateFactory is missing for aggregate type '{aggregateType}'.");
			}

			foreach (var wrappedEvent in events)
			{
				aggregate.Apply(wrappedEvent.DomainEvent);
			}

			return aggregate;
		}

		private async Task<IReadOnlyList<WrappedEvent>> ListEventsAsync(string aggregateType, string aggregateId)
		{
			IReadOnlyList<WrappedEvent> events =
				await EventRepository.ListEventsAsync(aggregateType, aggregateId).ConfigureAwait(false);
			return events;
		}

		// Hook for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Prepared()
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
