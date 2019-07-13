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

namespace WalletWasabi.Hwi2.ProcessBridge
{
	public class HwiProcessBridge : IProcessBridge
	{
		public async Task<(string response, int exitCode)> SendCommandAsync(string arguments, CancellationToken cancel)
		{
			string responseString;
			int exitCode;
			string hwiPath = GetHwiPath();
			using (var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = hwiPath,
					Arguments = arguments,
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden
				}
			))
			{
				await process.WaitForExitAsync(cancel).ConfigureAwait(false);

				exitCode = process.ExitCode;
				responseString = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
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
