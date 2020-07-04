using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Microservices
{
	public class ProcessInvoker
	{
		public async Task<(string response, int exitCode)> SendCommandAsync(Process process, CancellationToken cancel)
		{
			// "using" makes sure the process exits at the end of this method.
			using var processAsync = new ProcessAsync(process);

			process.Start();

			await processAsync.WaitForExitAsync(cancel);

			string responseString = process.StartInfo.UseShellExecute
				? string.Empty
				: await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);

			return (responseString, exitCode: process.ExitCode);
		}
	}
}
