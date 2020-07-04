using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Microservices;

namespace WalletWasabi.Hwi.ProcessBridge
{
	public class HwiProcessBridge : IHwiProcessInvoker
	{
		public HwiProcessBridge(string processPath, ProcessInvoker processInvoker)
		{
			ProcessPath = processPath;
			ProcessInvoker = processInvoker;
		}

		private string ProcessPath { get; }
		private ProcessInvoker ProcessInvoker { get; }

		public async Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel)
		{
			var process = ProcessBuilder.BuildProcessInstance(ProcessPath, arguments, openConsole);

			(string rawResponse, int exitCode) = await ProcessInvoker.SendCommandAsync(process, cancel).ConfigureAwait(false);

			string response;

			if (!openConsole)
			{
				response = rawResponse;
			}
			else
			{
				response = exitCode == 0
					? "{\"success\":\"true\"}"
					: $"{{\"success\":\"false\",\"error\":\"Process terminated with exit code: {exitCode}.\"}}";
			}

			return (response, exitCode);
		}
	}

	public interface IHwiProcessInvoker
	{
		Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel);
	}
}
