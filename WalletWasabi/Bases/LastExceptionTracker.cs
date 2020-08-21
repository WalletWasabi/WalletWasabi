using System;
using WalletWasabi.Logging;

namespace WalletWasabi.Bases
{
	/// <summary>
	/// Tracker that stores the latest received exception, and increases a counter as long as the same exception type is received.
	/// </summary>
	public class LastExceptionTracker
	{
		private ExceptionInfo LastException { get; set; } = new ExceptionInfo();

		/// <summary>
		/// Process encountered exception and return the latest exception info.
		/// </summary>
		/// <returns>The latest exception.</returns>
		public void Process(Exception currentException) =>
			LastException = LastException switch
			{
				{ ExceptionCount: 0 } => LastException.First(currentException),
				{ Exception: {} ex } when ex.GetType() == currentException.GetType() && ex.Message == currentException.Message => LastException.Repeat(),
				_ => LastException
			};

		public void FinalizeExceptionsProcessing()
		{
			// Log previous exception if any.
			if (LastException.ExceptionCount > 0)
			{
				Logger.LogInfo(LastException.ToString());
				LastException = new ExceptionInfo();
			}
		}
	}
}
