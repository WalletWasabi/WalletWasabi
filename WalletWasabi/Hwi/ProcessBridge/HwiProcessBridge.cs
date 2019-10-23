using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;

namespace WalletWasabi.Hwi.ProcessBridge
{
	public class HwiProcessBridge : IProcessBridge
	{
		public async Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel)
		{
			string responseString;
			int exitCode;
			string hwiPath = EnvironmentHelpers.GetBinaryPath("Hwi", "hwi");

			var fileName = hwiPath;
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
				FileName = fileName,
				Arguments = finalArguments,
				RedirectStandardOutput = redirectStandardOutput,
				UseShellExecute = useShellExecute,
				CreateNoWindow = createNoWindow,
				WindowStyle = windowStyle
			};

			try
			{
				using var process = Process.Start(startInfo);
				await process.WaitForExitAsync(cancel).ConfigureAwait(false);

				exitCode = process.ExitCode;
				if (redirectStandardOutput)
				{
					responseString = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
				}
				else
				{
					responseString = exitCode == 0
						? "{\"success\":\"true\"}"
						: $"{{\"success\":\"false\",\"error\":\"Process terminated with exit code: {exitCode}.\"}}";
				}
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

			return (responseString, exitCode);
		}
	}
}
