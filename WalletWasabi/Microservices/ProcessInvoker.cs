using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Microservices
{
	public class ProcessInvoker
	{
		public async Task<(string response, int exitCode)> SendCommandAsync(ProcessStartInfo startInfo, CancellationToken token, Action<StreamWriter> standardInputWriter = null)
		{
			// "using" makes sure the process exits at the end of this method.
			using var processAsync = new ProcessAsync(startInfo);

			if (standardInputWriter is { })
			{
				processAsync.StartInfo.RedirectStandardInput = true;
			}

			processAsync.Start();

			if (standardInputWriter is { })
			{
				standardInputWriter(processAsync.StandardInput);
				processAsync.StandardInput.Close();
			}

			Task<string> readPipeTask = processAsync.StartInfo.UseShellExecute
				? Task.FromResult(string.Empty)
				: processAsync.StandardOutput.ReadToEndAsync();

			await processAsync.WaitForExitAsync(token);

			string output = await readPipeTask.ConfigureAwait(false);

			return (output, exitCode: processAsync.ExitCode);
		}
	}
}
