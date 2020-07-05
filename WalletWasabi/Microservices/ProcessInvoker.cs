using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Microservices
{
	public class ProcessInvoker
	{
		public async Task<(string response, int exitCode)> SendCommandAsync(Process process, CancellationToken cancel, Action<StreamWriter> standardInputWriter = null)
		{
			// "using" makes sure the process exits at the end of this method.
			using var processAsync = new ProcessAsync(process);

			if (standardInputWriter is { })
			{
				processAsync.StartInfo.RedirectStandardInput = true;
			}

			process.Start();

			if (standardInputWriter is { })
			{
				standardInputWriter(process.StandardInput);
				process.StandardInput.Close();
			}

			Task<string> readPipeTask = process.StartInfo.UseShellExecute
				? Task.FromResult(string.Empty)
				: process.StandardOutput.ReadToEndAsync();

			await processAsync.WaitForExitAsync(cancel);

			string output = await readPipeTask.ConfigureAwait(false);

			return (output, exitCode: process.ExitCode);
		}
	}
}
