using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Helpers
{
	public static class StartupHelper
	{
		private const string KeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

		// Arguments to add Wasabi to macOS's startup settings.
		private static readonly string AddArgument = $"-c \"osascript -e \' tell application \\\"System Events\\\" to make new login item at end of login items with properties {{name:\\\"{nameof(WalletWasabi)}\\\", path:\\\"/Applications/WasabiWallet.app\\\",hidden:false}} \' \"";

		// Argument to delete Wasabi from macOS startup settings.
		private static readonly string DeleteArgument = $"-c \"osascript -e \' tell application \\\"System Events\\\" to delete login item \\\"{nameof(WalletWasabi)}\\\" \' \"";

		public static void ModifyStartupSetting(bool runOnSystemStartup)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				string pathToExeFile = EnvironmentHelpers.GetExecutablePath();
				if (!File.Exists(pathToExeFile))
				{
					throw new InvalidOperationException($"Path {pathToExeFile} does not exist.");
				}
				StartOnWindowsStartup(runOnSystemStartup, pathToExeFile);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				throw new NotImplementedException();
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				StartOnMacStartUp(runOnSystemStartup);
			}
		}

		private static void StartOnWindowsStartup(bool runOnSystemStartup, string pathToExeFile)
		{
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				throw new InvalidOperationException("Registry modification can only be done on Windows.");
			}

			using RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true) ?? throw new InvalidOperationException("Registry operation failed.");
			if (runOnSystemStartup)
			{
				key.SetValue(nameof(WalletWasabi), pathToExeFile);
			}
			else
			{
				key.DeleteValue(nameof(WalletWasabi), false);
			}
		}

		private static void StartOnMacStartUp(bool runOnSystemStartup)
		{
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				throw new InvalidOperationException("Running osascript can only be done on macOS.");
			}

			ProcessStartInfo processInfo = new()
			{
				UseShellExecute = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				FileName = "/bin/bash",
				CreateNoWindow = false,
				Arguments = runOnSystemStartup ? AddArgument : DeleteArgument
			};

			Process.Start(processInfo);
		}
	}
}
