using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing.Exceptions
{
	public class CommandFailedException : ApplicationException, ISerializable
	{
		public long LastSequenceId { get; init; }
		public IState State { get; init; }
		public IReadOnlyList<IError> Errors { get; init; }
		public ICommand Command { get; init; }
		public string AggregateType { get; init; }
		public string AggregateId { get; init; }

		public CommandFailedException(
			string aggregateType,
			string aggregateId,
			long lastSequenceId,
			IState state,
			ICommand command,
			IReadOnlyList<IError> errors)
		: base(
				AppendErrors(aggregateType, aggregateId, lastSequenceId, command, errors))
		{
			AggregateType = aggregateType;
			AggregateId = aggregateId;
			LastSequenceId = lastSequenceId;
			State = state;
			Command = command;
			Errors = errors;
		}

		public CommandFailedException(
			string aggregateType,
			string aggregateId,
			long lastSequenceId,
			IState state,
			ICommand command,
			IReadOnlyList<IError> errors,
			string? message)
		: base(
				AppendErrors(aggregateType, aggregateId, lastSequenceId, command, errors, message))
		{
			AggregateType = aggregateType;
			AggregateId = aggregateId;
			LastSequenceId = lastSequenceId;
			State = state;
			Command = command;
			Errors = errors;
		}

		public CommandFailedException(
			string aggregateType,
			string aggregateId,
			long lastSequenceId,
			IState state,
			ICommand command,
			IReadOnlyList<IError> errors,
			string? message,
			Exception? innerException)
		: base(
				AppendErrors(aggregateType, aggregateId, lastSequenceId, command, errors, message),
				innerException)
		{
			AggregateType = aggregateType;
			AggregateId = aggregateId;
			LastSequenceId = lastSequenceId;
			State = state;
			Command = command;
			Errors = errors;
		}

		public static string AppendErrors(string aggregateType, string aggregateId, long lastSequenceId, ICommand command, IReadOnlyList<IError> errors, string? message = null)
		{
			var builder = new StringBuilder();
			if (!string.IsNullOrEmpty(message))
			{
				builder.AppendLine(message);
				builder.AppendLine();
				builder.AppendLine();
			}
			builder.AppendLine($"Command '{command.GetType().Name}' has failed on aggregate version: '{aggregateType}/{aggregateId}/{lastSequenceId}'");
			builder.AppendLine();
			builder.AppendLine($"Last event SequenceId: {lastSequenceId}");
			builder.AppendLine();
			builder.AppendLine($"Errors:");
			foreach (var error in errors)
			{
				builder.AppendLine($"\"{error.PropertyName}\": \"{error.ErrorMessage}\"");
			}
			return builder.ToString();
		}

		public override IDictionary Data => new Dictionary<string, object>
		{
			[nameof(LastSequenceId)] = LastSequenceId,
			[nameof(State)] = State,
			[nameof(Errors)] = Errors,
			[nameof(Command)] = Command,
		};
	}
}
