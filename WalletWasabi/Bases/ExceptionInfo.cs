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
			: this (new Exception(), firstAppeared: DateTimeOffset.UtcNow, count: 0)
		{
		}

		public Exception Exception { get; }
		public long ExceptionCount { get; }
		public DateTimeOffset FirstAppeared { get; }

		public ExceptionInfo First(Exception exception) =>
			new ExceptionInfo(exception, DateTimeOffset.UtcNow, 1);

		public ExceptionInfo Repeat() =>
			new ExceptionInfo(Exception, FirstAppeared, ExceptionCount + 1);

		public override string ToString() =>
			$"Exception stopped coming. It came for " +
			$"{(DateTimeOffset.UtcNow - FirstAppeared).TotalSeconds} seconds, " +
			$"{ExceptionCount} times: {Exception.ToTypeMessageString()}";
	}
}
