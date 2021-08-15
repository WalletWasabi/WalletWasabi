using Microsoft.Win32;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

namespace WalletWasabi.Helpers
{
	public static class WindowsStartupChecker
	{
		private const string KeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

		[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Platform must be checked by the caller.")]
		public static bool CheckRegistryKeyExists()
		{
			bool result = false;

			RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false) ?? throw new InvalidOperationException("Registry operation failed.");
			result = registryKey.GetValueNames().Contains(nameof(WalletWasabi));

			return result;
		}
	}
}
