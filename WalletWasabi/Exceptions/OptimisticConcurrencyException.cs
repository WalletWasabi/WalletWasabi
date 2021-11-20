using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Exceptions
{
	/// <summary>
	/// Conflict occured while commiting database transaction.
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
