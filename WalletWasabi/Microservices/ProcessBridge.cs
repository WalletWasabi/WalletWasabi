using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Microservices
{
	public class ProcessBridge : IProcessBridge
	{
		public ProcessBridge(string processPath)
		{
			ProcessPath = Guard.NotNullOrEmptyOrWhitespace(nameof(processPath), processPath);

			if (!File.Exists(ProcessPath))
			{
				throw new FileNotFoundException($"{Path.GetFileNameWithoutExtension(ProcessPath)} is not found.", ProcessPath);
			}
		}

		public string ProcessPath { get; }

		public async Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel)
		{
			// "using" makes sure the process exits at the end of this method.
			using var process = new ProcessAsync(ProcessBuilder.BuildProcessInstance(ProcessPath, arguments, openConsole));

			process.Start();

			await process.WaitForExitAsync(cancel);

			string responseString = openConsole ? string.Empty : await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
			return (responseString, exitCode: process.ExitCode);
		}
	}
}
