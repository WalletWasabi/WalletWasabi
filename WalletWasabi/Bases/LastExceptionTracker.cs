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
			if (!(LastException is null)
				&& currentException.GetType() == LastException.Exception.GetType()
				&& currentException.Message == LastException.Exception.Message)
			{
				// Increment the counter.
				LastException.ExceptionCount++;
				return null;
			}
			else
			{
				var previousException = LastException;
				LastException = new ExceptionInfo(currentException, exceptionCount: 1, firstAppeared: DateTimeOffset.UtcNow);
				return previousException;
			}
		}
	}
}
