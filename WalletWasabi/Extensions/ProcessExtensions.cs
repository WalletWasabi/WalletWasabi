using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Diagnostics
{
	public static class ProcessExtensions
	{
		public static async Task WaitForExitAsync(this Process process, CancellationToken cancellationToken)
		{
			while (!process.HasExited)
			{
				await Task.Delay(100, cancellationToken).ConfigureAwait(false); // TODO: https://github.com/zkSNACKs/WalletWasabi/issues/1452
			}
		}
	}
}
