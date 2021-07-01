using Microsoft.Win32;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using WalletWasabi.Logging;

namespace WalletWasabi.Helpers
{
	public static class StartupHelper
	{
		public const string StartupErrorMessage = "Something went wrong while trying to make your changes.";
		private const string KeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

		public static bool TryModifyStartupSetting(bool runOnSystemStartup)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				string pathToExeFile = Assembly.GetExecutingAssembly().Location;
				pathToExeFile = pathToExeFile.Remove(pathToExeFile.Length - 4);        // This part has to change if this gets released
				pathToExeFile += ".Fluent.Desktop.exe";

				return TryModifyRegistry(runOnSystemStartup, pathToExeFile);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				// Method call here
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				// Method call here
			}
			return false;
		}

		[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Method can be called only on Windows.")]
		private static bool TryModifyRegistry(bool runOnSystemStartup, string pathToExeFile)
		{
			try
			{
				using RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true) ?? throw new NullReferenceException();
				if (runOnSystemStartup)
				{
					key.SetValue("WasabiWallet", pathToExeFile);
				}
				else
				{
					key.DeleteValue("WasabiWallet");
				}

				return true;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}

			return false;
		}
	}
}
