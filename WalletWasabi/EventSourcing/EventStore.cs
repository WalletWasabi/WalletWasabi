using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.ArenaDomain;
using WalletWasabi.EventSourcing.ArenaDomain.Command;
using WalletWasabi.EventSourcing.ArenaDomain.CommandProcessor;
using WalletWasabi.EventSourcing.ArenaDomain.Events;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Exceptions;
using WalletWasabi.Interfaces.EventSourcing;

namespace WalletWasabi.EventSourcing
{
	public class EventStore : IEventStore
	{
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

		public async Task ProcessCommandAsync(ICommand command, string aggregateType, string aggregateId)
		{
			int tries = 3;
			bool success = false;
			do
			{
				try
				{
					var events = await GetEventsAsync(aggregateType, aggregateId).ConfigureAwait(false);
					var aggregate = ApplyEvents(aggregateType, events);

					bool commandAlreadyProcessed = events.Any(ev => ev.SourceId == command.IdempotenceId);
					if (commandAlreadyProcessed)
					{
						return;
					}

					if (!CommandProcessorFactory.TryGetValue(aggregateType, out var commandProcessorFactory))
					{
						throw new InvalidOperationException($"CommandProcessor is missing for aggregate type '{aggregateType}'.");
					}

					ICommandProcessor processor = commandProcessorFactory.Invoke();
					var newEvents = processor.Process(command, aggregate.State);

					var lastEvent = events.Any() ? events[^1] : null;
					var sequenceId = lastEvent == null ? 1 : lastEvent.SequenceId + 1;
					List<WrappedEvent> wrappedEvents = new();
					foreach (var newEvent in newEvents)
					{
						wrappedEvents.Add(new WrappedEvent(sequenceId, newEvent, command.IdempotenceId));
						sequenceId++;
					}

					await EventRepository.AppendEventsAsync(aggregateType, aggregateId, wrappedEvents)
						.ConfigureAwait(false);
					success = true;
				}
				catch (OptimisticConcurrencyException)
				{
					if (tries <= 0)
					{
						throw;
					}

					tries--;
				}
			} while (!success);
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
