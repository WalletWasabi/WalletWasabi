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
			string hwiPath = GetHwiPath();

			var fileName = hwiPath;
			var finalArguments = arguments;
			var redirectStandardOutput = !openConsole;
			var useShellExecute = openConsole;
			var createNoWindow = !openConsole;
			var windowStyle = openConsole ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden;

			if (openConsole && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				var escapedArguments = (hwiPath + " " + arguments).Replace("\"", "\\\"");
				useShellExecute = false;
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					fileName = "xterm";
					finalArguments = $"-c \"{escapedArguments}\"";
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					fileName = "osascript";
					finalArguments = $"-e \"{escapedArguments}\"";
				}
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
				using (var process = Process.Start(
					startInfo
				))
				{
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

		private string GetHwiPath()
		{
			var fullBaseDirectory = EnvironmentHelpers.GetFullBaseDirectory();

			string commonPartialPath = Path.Combine(fullBaseDirectory, "Hwi", "Binaries");
			string path;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				path = Path.Combine(commonPartialPath, "hwi-win64", "hwi.exe");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				path = Path.Combine(commonPartialPath, "hwi-lin64", "hwi");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				path = Path.Combine(commonPartialPath, "hwi-osx64", "hwi");
			}
			else
			{
				throw new NotSupportedException("Operating system is not supported.");
			}

			return path;
		}
	}
}
