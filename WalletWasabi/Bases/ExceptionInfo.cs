using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Bases
{
	public class ExceptionInfo
	{
		public ExceptionInfo(Exception exception, long exceptionCount, DateTimeOffset firstAppeared)
		{
			Exception = exception;
			ExceptionCount = exceptionCount;
			FirstAppeared = firstAppeared;
		}

		public Exception Exception { get; }
		public long ExceptionCount { get; set; }
		public DateTimeOffset FirstAppeared { get; }
	}
}
