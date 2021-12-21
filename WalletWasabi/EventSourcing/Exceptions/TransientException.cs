using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.EventSourcing.Exceptions
{
	/// <summary>
	/// Infrastructure or optimistic conflict exception that will be fixed when retried.
	/// </summary>
	public class TransientException : Exception
	{
		public TransientException()
		{
		}

		public TransientException(string? message) : base(message)
		{
		}

		public TransientException(string? message, Exception? innerException) : base(message, innerException)
		{
		}

		protected TransientException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}
