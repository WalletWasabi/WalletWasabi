using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Microservices
{
	public class ProcessInvoker
	{
		public async Task<(string response, int exitCode)> SendCommandAsync(ProcessStartInfo startInfo, CancellationToken token, Action<StreamWriter>? standardInputWriter = null)
		{
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

			Task<string> readErrorPipeTask = processAsync.StartInfo.UseShellExecute
				? Task.FromResult(string.Empty)
				: processAsync.StandardError.ReadToEndAsync();

			await processAsync.WaitForExitAsync(token).ConfigureAwait(false);

			string output = await readPipeTask.ConfigureAwait(false);
			string error = await readErrorPipeTask.ConfigureAwait(false);

			if (!string.IsNullOrEmpty(error))
			{
				throw new InvalidOperationException($"output:{output} error:{error}");
			}

			return (output, exitCode: processAsync.ExitCode);
		}
	}
}
