using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BundledApps;

namespace WalletWasabi.Hwi.ProcessBridge;

public class HwiProcessBridge : IHwiProcessInvoker
{
	public HwiProcessBridge()
	{
		_processPath = BundledAppHelpers.GetBinaryPath("hwi");
	}

	private readonly string _processPath;

	public async Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel, Action<StreamWriter>? standardInputWriter = null)
	{
		ProcessStartInfo startInfo = ProcessStartInfoFactory.Make(_processPath, arguments, openConsole);

		(string rawResponse, int exitCode) = await SendCommandAsync(startInfo, cancel, standardInputWriter).ConfigureAwait(false);

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

	private async Task<(string response, int exitCode)> SendCommandAsync(ProcessStartInfo startInfo, CancellationToken token, Action<StreamWriter>? standardInputWriter = null)
	{
		using var process = new Process()
		{
			StartInfo = startInfo
		};

		if (standardInputWriter is { })
		{
			process.StartInfo.RedirectStandardInput = true;
		}

		process.StartWithExceptionLogging();

		if (standardInputWriter is { })
		{
			standardInputWriter(process.StandardInput);
			process.StandardInput.Close();
		}

		Task<string> readPipeTask = process.StartInfo.UseShellExecute
			? Task.FromResult(string.Empty)
			: process.StandardOutput.ReadToEndAsync();

		await process.GracefulWaitForExitAsync(token).ConfigureAwait(false);

		string output = await readPipeTask.ConfigureAwait(false);

		return (output, exitCode: process.ExitCode);
	}
}
