using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Helpers;

namespace WalletWasabi.EventSourcing
{
	/// <summary>
	/// Domain command result
	/// </summary>
	public record Result
	{
		public bool Success { get; init; }
		public bool Empty { get; init; }
		public IReadOnlyList<IEvent> Events { get; init; } = Array.Empty<IEvent>();
		public IReadOnlyList<IError> Errors { get; init; } = Array.Empty<IError>();

		public Result(IReadOnlyList<IEvent> events)
		{
			Guard.NotNull(nameof(events), events);
			Success = true;
			Empty = events.Count <= 0;
			Events = events;
		}

		public Result(IReadOnlyList<IError> errors)
		{
			Guard.NotNullOrEmpty(nameof(errors), errors);
			Success = false;
			Errors = errors;
		}

		public Result(ImmutableArray<IEvent>.Builder events) : this(events.ToImmutable()) { }

		public Result(ImmutableArray<IError>.Builder errors) : this(errors.ToImmutable()) { }

		public Result(IEnumerable<IEvent> events) : this(CreateImmutableArray(events)) { }

		public Result(IEnumerable<IError> errors) : this(CreateImmutableArray(errors)) { }

		public Result(IEvent @event) : this(CreateImmutableArray(@event)) { }

		public Result(IError error) : this(CreateImmutableArray(error)) { }

		public static Result Succeed(IReadOnlyList<IEvent> events)
		{
			return new Result(events);
		}

		public static Result Succeed(ImmutableArray<IEvent>.Builder events)
		{
			return new Result(events);
		}

		public static Result Succeed(IEnumerable<IEvent> events)
		{
			return new Result(events);
		}

		public static Result Succeed(IEvent @event)
		{
			return new Result(@event);
		}

		public static Result Fail(IReadOnlyList<IError> errors)
		{
			return new Result(errors);
		}

		public static Result Fail(ImmutableArray<IError>.Builder errors)
		{
			return new Result(errors);
		}

		public static Result Fail(IEnumerable<IError> errors)
		{
			return new Result(errors);
		}

		public static Result Fail(IError @error)
		{
			return new Result(@error);
		}

		private static ImmutableArray<T> CreateImmutableArray<T>(IEnumerable<T> items)
		{
			var builder = ImmutableArray.CreateBuilder<T>();
			builder.AddRange(items);
			return builder.ToImmutable();
		}
		private static ImmutableArray<T> CreateImmutableArray<T>(T item)
		{
			return ImmutableArray.Create(item);
		}
	}
}
