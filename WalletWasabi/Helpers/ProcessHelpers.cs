using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Helpers
{
	public static class ProcessHelpers
	{
		public static async Task WaitForExitAsync(this Process process, CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				await Task.Delay(500);
				if (process.HasExited) break;
			}
		}
	}
}
