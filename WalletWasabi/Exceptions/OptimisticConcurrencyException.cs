using System.Runtime.Serialization;

namespace WalletWasabi.Exceptions
{
	/// <summary>
	/// Conflict occurred while committing database transaction.
	/// Transaction needs to be retried.
	/// </summary>
	public class OptimisticConcurrencyException : TransientException
	{
		public OptimisticConcurrencyException()
		{
		}

		public OptimisticConcurrencyException(string? message) : base(message)
		{
		}

		public OptimisticConcurrencyException(string? message, Exception? innerException) : base(message, innerException)
		{
		}

		protected OptimisticConcurrencyException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}
