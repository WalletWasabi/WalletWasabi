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

		public static void ModifyStartupSetting(bool runOnSystemStartup)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				string pathToExeFile = EnvironmentHelpers.GetExecutablePath();
				if (!File.Exists(pathToExeFile))
				{
					throw new InvalidOperationException($"Path {pathToExeFile} does not exist.");
				}
				ModifyRegistry(runOnSystemStartup, pathToExeFile);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				throw new NotImplementedException();
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				ModifyMacOsLoginItems(runOnSystemStartup);
			}
		}

		private static void ModifyRegistry(bool runOnSystemStartup, string pathToExeFile)
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

		private static void ModifyMacOsLoginItems(bool runOnSystemStartup)
		{
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				throw new InvalidOperationException("Running osascript can only be done on macOS.");
			}

			string argumentToAddWasabiToMacOsStartupSetting = $"-c \"osascript -e \' tell application \\\"System Events\\\" to make new login item at end of login items with properties {{name:\\\"{nameof(WalletWasabi)}\\\", path:\\\"/Applications/WasabiWallet.app\\\",hidden:false}} \' \"";
			string argumentToDeleteWasabiFromMacOsStartupSetting = $"-c \"osascript -e \' tell application \\\"System Events\\\" to delete login item \\\"{nameof(WalletWasabi)}\\\" \' \"";

			ProcessStartInfo processInfo = new()
			{
				UseShellExecute = false,
				WindowStyle = ProcessWindowStyle.Hidden,
				FileName = "/bin/bash",
				CreateNoWindow = false,
				Arguments = runOnSystemStartup ? argumentToAddWasabiToMacOsStartupSetting : argumentToDeleteWasabiFromMacOsStartupSetting
			};

			Process.Start(processInfo);
		}
	}
}
