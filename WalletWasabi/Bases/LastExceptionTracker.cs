using System;

namespace WalletWasabi.Bases
{
	public class LastExceptionTracker
	{
		public ExceptionInfo? LastException { get; set; }

		/// <summary>
		/// Process encountered exception.
		/// </summary>
		/// <returns>Previous exception or <c>null</c>.</returns>
		public ExceptionInfo? Process(Exception currentException)
		{
			// Only log one type of exception once.
			if (LastException is null // If the exception never came.
				|| currentException.GetType() != LastException.Exception.GetType() // Or the exception has different type from previous exception.
				|| currentException.Message != LastException.Exception.Message) // Or the exception has different message from previous exception.
			{
				var previousException = LastException;
				LastException = new ExceptionInfo(currentException, exceptionCount: 1, firstAppeared: DateTimeOffset.UtcNow);
				return previousException;
			}
			else
			{
				// Increment the counter.
				LastException.ExceptionCount++;
				return null;
			}
		}
	}
}
