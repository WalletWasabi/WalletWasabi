using System;
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

		public CommandFailedException(
			IReadOnlyList<IError> errors,
			long lastSequenceId,
			IState state)
		{
			Errors = errors;
			LastSequenceId = lastSequenceId;
			State = state;
		}

		public CommandFailedException(
			IReadOnlyList<IError> errors,
			long lastSequenceId,
			IState state,
			string? message) : base(AppendErrors(message, lastSequenceId, errors))
		{
			Errors = errors;
			LastSequenceId = lastSequenceId;
			State = state;
		}

		public CommandFailedException(
			IReadOnlyList<IError> errors,
			long lastSequenceId,
			IState state,
			string? message,
			Exception? innerException) : base(AppendErrors(message, lastSequenceId, errors), innerException)
		{
			Errors = errors;
			LastSequenceId = lastSequenceId;
			State = state;
		}

		public static string AppendErrors(string? message, long lastSequenceId, IReadOnlyList<IError> errors)
		{
			var builder = new StringBuilder(message ?? "");
			builder.AppendLine();
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
	}
}
