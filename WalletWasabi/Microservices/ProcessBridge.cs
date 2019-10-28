using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Microservices
{
	public class ProcessBridge : IProcessBridge
	{
		public string ProcessPath { get; }

		public ProcessBridge(string processPath)
		{
			ProcessPath = Guard.NotNullOrEmptyOrWhitespace(nameof(processPath), processPath);
			if (!File.Exists(ProcessPath))
			{
				var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(ProcessPath);
				throw new FileNotFoundException($"{fileNameWithoutExtension} is not found.", ProcessPath);
			}
		}

		public Process Start(string arguments, bool openConsole)
		{
			var finalArguments = arguments;
			var redirectStandardOutput = !openConsole;
			var useShellExecute = openConsole;
			var createNoWindow = !openConsole;
			var windowStyle = openConsole ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden;

			if (openConsole && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				throw new PlatformNotSupportedException($"{RuntimeInformation.OSDescription} is not supported.");
				//var escapedArguments = (hwiPath + " " + arguments).Replace("\"", "\\\"");
				//useShellExecute = false;
				//if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				//{
				//	fileName = "xterm";
				//	finalArguments = $"-e \"{escapedArguments}\"";
				//}
				//else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				//{
				//	fileName = "osascript";
				//	finalArguments = $"-e 'tell application \"Terminal\" to do script \"{escapedArguments}\"'";
				//}
			}

			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = ProcessPath,
				Arguments = finalArguments,
				RedirectStandardOutput = redirectStandardOutput,
				UseShellExecute = useShellExecute,
				CreateNoWindow = createNoWindow,
				WindowStyle = windowStyle
			};

			try
			{
				return Process.Start(startInfo);
			}
			catch
			{
				Logger.LogInfo($"{nameof(startInfo.FileName)}: {startInfo.FileName}");
				Logger.LogInfo($"{nameof(startInfo.Arguments)}: {startInfo.Arguments}");
				Logger.LogInfo($"{nameof(startInfo.RedirectStandardOutput)}: {startInfo.RedirectStandardOutput}");
				Logger.LogInfo($"{nameof(startInfo.UseShellExecute)}: {startInfo.UseShellExecute}");
				Logger.LogInfo($"{nameof(startInfo.CreateNoWindow)}: {startInfo.CreateNoWindow}");
				Logger.LogInfo($"{nameof(startInfo.WindowStyle)}: {startInfo.WindowStyle}");

				throw;
			}
		}

		public async Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel)
		{
			string responseString;
			int exitCode;

			using var process = Start(arguments, openConsole);
			await process.WaitForExitAsync(cancel).ConfigureAwait(false);

			exitCode = process.ExitCode;
			if (!openConsole)
			{
				responseString = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
			}
			else
			{
				responseString = string.Empty;
			}

			return (responseString, exitCode);
		}
	}
}
