using System;
using System.Collections.Generic;
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
			Empty = events.Any();
			Events = events;
		}

		public Result(IReadOnlyList<IError> errors)
		{
			Guard.NotNullOrEmpty(nameof(errors), errors);
			Success = false;
			Errors = errors;
		}

		public Result(IEvent @event) : this(new List<IEvent> { @event }.AsReadOnly())
		{
		}

		public Result(IError error) : this(new List<IError> { error }.AsReadOnly())
		{
		}

		public static Result Succeed(IReadOnlyList<IEvent> events)
		{
			return new Result(events);
		}

		public static Result Fail(IReadOnlyList<IError> errors)
		{
			return new Result(errors);
		}
	}
}
