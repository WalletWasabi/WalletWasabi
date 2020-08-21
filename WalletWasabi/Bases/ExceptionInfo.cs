using System;

namespace WalletWasabi.Bases
{
	public class ExceptionInfo
	{
		public ExceptionInfo(Exception exception)
		{
			Exception = exception;
			ExceptionCount = 1;
			FirstAppeared = DateTimeOffset.UtcNow;
		}

		public Exception Exception { get; }
		public long ExceptionCount { get; set; }
		public DateTimeOffset FirstAppeared { get; }
	}
}
