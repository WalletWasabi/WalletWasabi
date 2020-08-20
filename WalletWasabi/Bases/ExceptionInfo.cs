using System;

namespace WalletWasabi.Bases
{
	public class ExceptionInfo
	{
		private ExceptionInfo(Exception exception, DateTimeOffset firstAppeared, long count)
		{
			Exception = exception;
			ExceptionCount = count;
			FirstAppeared = firstAppeared;
		}

		public ExceptionInfo()
			: this (new Exception(), DateTimeOffset.UtcNow, 0)
		{
		}

		public Exception Exception { get; }
		public long ExceptionCount { get; }
		public DateTimeOffset FirstAppeared { get; }

		public ExceptionInfo Is(Exception exception) =>
			new ExceptionInfo(exception, DateTimeOffset.UtcNow, 1);

		public ExceptionInfo Again() =>
			new ExceptionInfo(Exception, FirstAppeared, ExceptionCount + 1);
	}
}
