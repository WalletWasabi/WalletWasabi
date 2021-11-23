using System;
using System.Runtime.Serialization;

namespace WalletWasabi.Exceptions
{
	/// <summary>
	/// Program is in an unexpected state and it failed internally
	/// </summary>
	public class AssertionFailedException : ApplicationException
	{
		public AssertionFailedException()
		{
		}

		public AssertionFailedException(string? message) : base(message)
		{
		}

		public AssertionFailedException(string? message, Exception? innerException) : base(message, innerException)
		{
		}

		protected AssertionFailedException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}
