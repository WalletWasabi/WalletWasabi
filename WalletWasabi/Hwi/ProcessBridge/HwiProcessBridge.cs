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

namespace WalletWasabi.Hwi.ProcessBridge
{
	public class HwiProcessBridge : IProcessBridge
	{
		public async Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel)
		{
			string responseString;
			int exitCode;
			string hwiPath = GetHwiPath();

			var redirectStandardOutput = !openConsole;
			var useShellExecute = openConsole;
			var createNoWindow = !openConsole;
			var windowStyle = openConsole ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden;

			using (var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = hwiPath,
					Arguments = arguments,
					RedirectStandardOutput = redirectStandardOutput,
					UseShellExecute = useShellExecute,
					CreateNoWindow = createNoWindow,
					WindowStyle = windowStyle
				}
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

			return (responseString, exitCode);
		}

		private string GetHwiPath()
		{
			var fullBaseDirectory = EnvironmentHelpers.GetFullBaseDirectory();

			string commonPartialPath = Path.Combine(fullBaseDirectory, "Hwi2", "Binaries");
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
