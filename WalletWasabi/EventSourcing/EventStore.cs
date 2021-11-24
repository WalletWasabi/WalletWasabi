using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.ArenaDomain;
using WalletWasabi.EventSourcing.ArenaDomain.Command;
using WalletWasabi.EventSourcing.ArenaDomain.CommandProcessor;
using WalletWasabi.EventSourcing.ArenaDomain.Events;
using WalletWasabi.EventSourcing.Exceptions;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Exceptions;
using WalletWasabi.Interfaces.EventSourcing;

namespace WalletWasabi.EventSourcing
{
	public class EventStore : IEventStore
	{
		public const int OptimisticRetryLimit = 10;

		private IEventRepository EventRepository { get; }

		private Dictionary<string, Func<IAggregate>> AggregateFactory { get; } = new()
		{
			[nameof(RoundAggregate)] = () => new RoundAggregate(),
		};

		private Dictionary<string, Func<ICommandProcessor>> CommandProcessorFactory { get; } = new()
		{
			[nameof(RoundAggregate)] = () => new RoundCommandProcessor(),
		};

		public EventStore(IEventRepository eventRepository)
		{
			EventRepository = eventRepository;
		}

		public async Task<WrappedResult> ProcessCommandAsync(ICommand command, string aggregateType, string aggregateId)
		{
			int tries = OptimisticRetryLimit + 1;
			bool optimisticConflict = false;
			do
			{
				tries--;
				optimisticConflict = false;
				try
				{
					var events = await GetEventsAsync(aggregateType, aggregateId).ConfigureAwait(false);
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

					if (!CommandProcessorFactory.TryGetValue(aggregateType, out var commandProcessorFactory))
					{
						throw new AssertionFailedException($"CommandProcessor is missing for aggregate type '{aggregateType}'.");
					}

					var processor = commandProcessorFactory.Invoke();
					var result = processor.Process(command, aggregate.State);

					if (result.Success)
					{
						List<WrappedEvent> wrappedEvents = new();
						foreach (var newEvent in result.Events)
						{
							sequenceId++;
							wrappedEvents.Add(new WrappedEvent(sequenceId, newEvent, command.IdempotenceId));
							aggregate.Apply(newEvent);
						}

						await EventRepository.AppendEventsAsync(aggregateType, aggregateId, wrappedEvents)
							.ConfigureAwait(false);

						return new WrappedResult(sequenceId, wrappedEvents.AsReadOnly(), aggregate.State);
					}
					else
					{
						throw new CommandFailedException(
							result.Errors,
							sequenceId,
							aggregate.State,
							$"Command '{command.GetType().Name}' has failed on aggregate version: '{aggregateType}/{aggregateId}/{sequenceId}'");
					}
				}
				catch (OptimisticConcurrencyException)
				{
					if (tries <= 0)
					{
						throw;
					}
					optimisticConflict = true;
				}
			} while (optimisticConflict && tries > 0);
			throw new AssertionFailedException($"Unexpected code reached in {nameof(ProcessCommandAsync)}");
		}

		public async Task<IAggregate> GetAggregateAsync(string aggregateType, string aggregateId)
		{
			var events = await GetEventsAsync(aggregateType, aggregateId).ConfigureAwait(false);

			return ApplyEvents(aggregateType, events);
		}

		private IAggregate ApplyEvents(string aggregateType, IReadOnlyList<WrappedEvent> events)
		{
			if (!AggregateFactory.TryGetValue(aggregateType, out var aggregateFactory))
			{
				throw new InvalidOperationException($"AggregateFactory is missing for aggregate type '{aggregateType}'.");
			}

			var aggregate = aggregateFactory.Invoke();

			foreach (var wrappedEvent in events)
			{
				aggregate.Apply(wrappedEvent.DomainEvent);
			}

			return aggregate;
		}

		private async Task<IReadOnlyList<WrappedEvent>> GetEventsAsync(string aggregateType, string aggregateId)
		{
			IReadOnlyList<WrappedEvent> events =
				await EventRepository.ListEventsAsync(aggregateType, aggregateId).ConfigureAwait(false);
			return events;
		}
	}
}
