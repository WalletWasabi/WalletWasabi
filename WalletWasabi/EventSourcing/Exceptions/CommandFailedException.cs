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

		public CommandFailedException(IReadOnlyList<IError> errors, long lastSequenceId, IState state)
		{
			Errors = errors;
			LastSequenceId = lastSequenceId;
			State = state;
		}

		public CommandFailedException(IReadOnlyList<IError> errors, long lastSequenceId, IState state, string? message) : base(message)
		{
			Errors = errors;
			LastSequenceId = lastSequenceId;
			State = state;
		}

		public CommandFailedException(IReadOnlyList<IError> errors, long lastSequenceId, IState state, string? message, Exception? innerException) : base(message, innerException)
		{
			Errors = errors;
			LastSequenceId = lastSequenceId;
			State = state;
		}
	}
}
