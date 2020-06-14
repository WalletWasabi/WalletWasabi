using System;
using System.Collections.Generic;
using System.Text;

namespace System.Threading
{
	public static class ThreadingHelpers
	{
		public static CancellationToken Cancelled
		{
			get
			{
				if (!CancelledBacking.HasValue)
				{
					using var cts = new CancellationTokenSource();
					cts.Cancel();
					CancelledBacking = cts.Token;
				}
				return CancelledBacking.Value;
			}
		}

		private static CancellationToken? CancelledBacking { get; set; } = null;
	}
}
