using System;
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
	}
}
