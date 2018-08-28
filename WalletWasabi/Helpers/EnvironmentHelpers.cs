using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using WalletWasabi.Logging;

namespace WalletWasabi.Helpers
{
	public static class EnvironmentHelpers
	{
		public static string GetDataDir(string appName)
		{
			string directory = null;

			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				var home = Environment.GetEnvironmentVariable("HOME");
				if (!string.IsNullOrEmpty(home))
				{
					directory = Path.Combine(home, "." + appName.ToLowerInvariant());
					Logger.LogInfo($"Using HOME environment variable for initializing application data at `{directory}`.");
				}
				else
				{
					throw new DirectoryNotFoundException("Could not find suitable datadir.");
				}
			}
			else
			{
				var localAppData = Environment.GetEnvironmentVariable("APPDATA");
				if (!string.IsNullOrEmpty(localAppData))
				{
					directory = Path.Combine(localAppData, appName);
					Logger.LogInfo($"Using APPDATA environment variable for initializing application data at `{directory}`.");
				}
				else
				{
					throw new DirectoryNotFoundException("Could not find suitable datadir.");
				}
			}

			if (Directory.Exists(directory)) return directory;

			Logger.LogInfo($"Creating data directory at `{directory}`.");
			Directory.CreateDirectory(directory);

			return directory;
		}

		/// <summary>
		/// Executes a command with bash.
		/// https://stackoverflow.com/a/47918132/2061103
		/// </summary>
		/// <param name="cmd"></param>
		public static void ShellExec(string cmd, bool waitForExit = true)
		{
			var escapedArgs = cmd.Replace("\"", "\\\"");

			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden,
					FileName = "/bin/sh",
					Arguments = $"-c \"{escapedArgs}\""
				}
			};

			process.Start();

			if (waitForExit)
			{
				process.WaitForExit();
			}
		}
	}
}
