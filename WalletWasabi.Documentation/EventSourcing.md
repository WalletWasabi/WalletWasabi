# Event Sourcing

## Concepts

* [Introduction](#introduction)
* Components (Business Domain)
  * [Event](#event)
  * [Aggregate](#aggregate)
  * [Command](#command)
  * [Command-Processor](#command-processor)
  * [Read-Model](#read-model)
* Theory
  * The Two Generals Problem
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
* Advanced Components (Business Domain)
  * Saga

### Introduction

### Event

### Aggregate

### Command

### Command-Processor

### Read-Model