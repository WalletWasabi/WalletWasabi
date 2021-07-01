using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WalletWasabi.Helpers
{
	public static class StartupHelper
	{
		public static void ModifyStartupSetting(bool isWasabiStartsWithOS)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				ModifyRegistry(isWasabiStartsWithOS);
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				// Method call here
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				// Method call here
			}
		}

		private static void ModifyRegistry(bool isWasabiStartsWithOS)
		{
			// This extra check is only to eliminate warnings.
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				string keyName = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
				using RegistryKey key = Registry.CurrentUser.OpenSubKey(keyName, true);
				if (isWasabiStartsWithOS)
				{
					string pathToExe = Assembly.GetExecutingAssembly().Location;
					pathToExe = pathToExe.Remove(pathToExe.Length - 4);        // This part has to change if this gets released
					pathToExe += ".Fluent.Desktop.exe";

					key.SetValue("WasabiWallet", pathToExe);
				}
				else
				{
					key.DeleteValue("WasabiWallet");
				}
			}
		}
	}
}
