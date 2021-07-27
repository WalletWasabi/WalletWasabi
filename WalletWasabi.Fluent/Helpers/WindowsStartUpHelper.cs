using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

namespace WalletWasabi.Fluent.Helpers
{
	public static class WindowsStartupHelper
	{
		private const string KeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

		public static void StartOnWindowsStartup(bool runOnSystemStartup, string pathToExeFile)
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
	}
}
