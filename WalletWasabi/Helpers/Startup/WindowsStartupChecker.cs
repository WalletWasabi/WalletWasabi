using Microsoft.Win32;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace WalletWasabi.Helpers
{
	public static class WindowsStartupChecker
	{
		private const string KeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

		public static bool CheckRegistryKeyExists()
		{
			bool result = false;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false) ?? throw new InvalidOperationException("Registry operation failed.");
				result = registryKey.GetValueNames().Contains(nameof(WalletWasabi));
			}

			return result;
		}
	}
}
