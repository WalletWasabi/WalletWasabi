# Event Sourcing

Event sourcing is an architectural pattern in which entities do not track their internal state by means of direct serialization or object-relational mapping, but by reading and committing events to an event store.

## Concepts

* [Introduction](#introduction)
* Components (Business Domain)
  * [Event](#event)
  * [Aggregate](#aggregate)
  * [Command](#command)
  * [Command-Processor](#command-processor)
  * [Read-Model](#read-model)
* Theory
  * The Two Generals' Problem
     * at-most-once strategy work-around
     * at-least-once strategy work-around
  * Eventual Consistency
  * Strong Serializable Consistency
  * IdempotenceId, SourceId, CorrelationId
* Infrastructure Components
  * EventRepository
  * EventStore
  * OnStartup Event re-delivery (at-least-once strategy)
  * PubSub Bus
  * Read-Model Updater
* Other
  * [Event-Storming](#event-storming)
* Advanced Components (Business Domain)
  * Saga
* [References](#references)

### Introduction

Event sourcing is an architectural pattern in which entities do not track their internal state by means of direct serialization or object-relational mapping, but by reading and committing events to an event store.

### Event

* Recording of an atomic action that has already happened in the past
* Strongly ordered sequence of events is the single source of truth. State of all entities is fully defined just by the sequence of events.
* naming conventions:
  * e.g.: InputAdded
  * Name ends with past tense verb
  * "Created", "Updated", "Deleted" are banned as names of events to prevent thinking in the CRUD mind-frame.
  * Names should be understandable and shared with business, marketing, users, developers to create common language and improve communication.
* Events have properties which carry state of the entity. Event's properties are equivalent to SQL Table's columns or object's fields in OOP.
* Event needs to be defined as an immutable serializable value object.
* next:
  * Process of defining and naming events by across team participation in a playful way with colorful sticky notes is called [Event Storming](#event-storming).
  * After event is persisted into its primary [Event Store](#event-store) it is published into [PubSub Bus](#pubsub-bus) so e.g. interested [Read-Model](#read-model) can eventually update itself to a new state.

### Aggregate

* Aggregate is a unique entity having a unique id
* Aggregate type is defined by a set of Event types
* All events belonging to the aggregate need to carry its id
* In code aggregate implements computation of its internal state from ordered sequence of events in `Apply(event)` method.
* Aggregates can create a hierarchy where one aggregate ownes a list of other aggregates. In such case id of a sub-aggregate consists of both its container aggregate-id and its own discriminating sub-aggregate id.
* All aggregate's events have guaranteed strong sequential consistency and all operations (commands) are atomic in the scope of an aggregate.
* next:
  * Above aggregate concept there is [Bounded Context](https://martinfowler.com/bliki/BoundedContext.html) which allows independed aggregates in the same bounded context to have strong sequential consistency and atomic operations across aggregate boundaries. Aggregates in different bounded contexts can have only [Eventual Consistency](#eventual-consistency) across.
  * For eventually consistent sequence of operations accross bounded contexts there is a concept of [Saga](#saga)

### Command

* Command is an input for a state transition of an [Aggregate](#aggregate).
* Command is implemented as an immutable serializable value object.
* Command is received and processed by the [Command-Processor](#command-processor).
* Command needs to contain [IdempotenceId](#idempotenceid) provided by the orignator of the command (client) to implement at-most-once work around of The Two Generals' Problem.
  * Second command with the same [IdempotenceId](#idempotenceid) is simply silently ignored.
* Issuing a command to a [Command-Processor](#command-processor) is the only legal way of transitioning state of an [Aggregate](#aggregate) and thus generating new [Events](#event).
* Commands are not persisted by default. Just events as a result of a successful command are persisted.
* Command can fail. In such case an originator is informed by an error result of the command. However command failure is not broadcasted to the [PubSub Bus](#pubsub-bus) for general audience because state transition didn't happen so there is nothing to broadcast. And command failure is not persisted by default.

### Command-Processor

* Command-Processor transitions state of an aggregate based on the [Command](#command) as an input and current internal state of the [Aggregate](#aggregate) by producing new [Events](#event).
* Mathematically it is a state machine transition rule.
* Command-Processor implementation must not have any side-effects. Command-Processor needs to be deterministic. The only input is [Command](#command) and internal [Aggregate](#aggregate) state and only output are new [Events](#event). Any external side-efect needs to be implemented externally as an reaction to a generated event by subscribing to a [PubSub Bus](#pubsub-bus).
  * One of the practical reasons for banning side-effects is because command can be retried again in case of an optimistic concurrency conflict detected upon persisting events.
* Command-Processor implementation can assume it is a single threaded and strongly sequentially serializable. Practical implementation is usually optimistic concurrency strategy where command is retried again on an up to date version of an [Aggregate](#aggregate) after conflict has been detected during persisting of [Events](#event).

### Read-Model

* Read-model handles all read-only clients' requests.
* Read-model is [Eventually consistent](#eventual-consistency). That means its state might be transitionally slightly out of date when compared to [Event Store](#event-store).
* Read-model has an internal copy of all the data. All queries are directly resolved from internal read-model state without touching the [Event Store](#event-store).
* Read-model's internal state is updated by [Events](#events) received from [PubSub Bus](#pubsub-bus)
* Read-model implementation must use at-least-once workaround of The Two Generals' Problem to guarantee no event is skipped.
* In general all queries should be `O(m*log(n))` at the worst where `n` is the size of the dataset and `m` is the response size. To achieve that any denormalization or redundancy of data is allowed.

### Eventual Consistency

* In an eventually consistent distributed system different services can have mutually inconsistent state for transitional period of time until all messages propagate through all the services of the system to achieve consistent state eventually in a bounded time.
* If not specified otherwise we use Strong Eventual consistency. Any two nodes that have received the same (unordered) set of updates will be in the same state.
  * That requires to deal with out of order event delivery and idempotency on event redelivery.
  * It must be implement using the at-least-once workaround of the Two Generals' Problem throughtout all the components of the system.
* source:
  * https://en.wikipedia.org/wiki/Eventual_consistency

### Event-Storming

* Event-Storming is a process of defining and naming events by across team participation in a playful way with colorful sticky notes.
* sources: 
  * https://en.wikipedia.org/wiki/Event_storming

### References

* The Two Generals' Problem (video): https://www.youtube.com/watch?v=IP-rGJKSZ3s
* EventFlow docs (C# EventSourcing lib; not used; inspiration): https://docs.geteventflow.net/
* EventSourcing theory:
  * wikipedia: https://en.wikipedia.org/wiki/Domain-driven_design#Event_sourcing
  * Martin Fowler: https://martinfowler.com/eaaDev/EventSourcing.html
  * Microservices.io: https://microservices.io/patterns/data/event-sourcing.html
  * Microsoft: https://docs.microsoft.com/en-us/azure/architecture/patterns/event-sourcing
  * EventStore.com: https://www.eventstore.com/event-sourcing
  * Bounded-Context: https://martinfowler.com/bliki/BoundedContext.html
* Event-Storming: https://en.wikipedia.org/wiki/Event_storming
* Eventual Consistency: https://en.wikipedia.org/wiki/Eventual_consistency