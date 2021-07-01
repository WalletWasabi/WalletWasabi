using Microsoft.Win32;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using WalletWasabi.Logging;

namespace WalletWasabi.Helpers
{
	public static class StartupHelper
	{
		public static bool TryModifyStartupSetting(bool isWasabiStartsWithOS)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return TryModifyRegistry(isWasabiStartsWithOS);
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				// Method call here
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				// Method call here
			}
			return false;
		}

		private static bool TryModifyRegistry(bool isWasabiStartsWithOS)
		{
			try
			{
				// This extra check is only to eliminate warnings.
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					string keyName = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
					using RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyName, writable: true);
					if (isWasabiStartsWithOS)
					{
						string pathToExeFile = Assembly.GetExecutingAssembly().Location;
						pathToExeFile = pathToExeFile.Remove(pathToExeFile.Length - 4);        // This part has to change if this gets released
						pathToExeFile += ".Fluent.Desktop.exe";

						key?.SetValue("WasabiWallet", pathToExeFile);
					}
					else
					{
						key?.DeleteValue("WasabiWallet");
					}

					return true;
				}
			}
			catch (ArgumentNullException ex)
			{
				Logger.LogError(ex);
			}
			catch (System.Security.SecurityException ex)
			{
				Logger.LogError("Permission to create registry entry is denied.", ex);
			}
			catch (ObjectDisposedException ex)
			{
				Logger.LogError("The RegistryKey is closed or cannot be accessed.", ex);
			}
			catch (NullReferenceException ex)
			{
				Logger.LogError("Couldn't open registry subkey in SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", ex);
			}

			return false;
		}
	}
}
