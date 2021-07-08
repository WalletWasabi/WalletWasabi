using Microsoft.Win32;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

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
					Logger.LogError($"Path {pathToExeFile} does not exist.");
				}
				if (!ModifyRegistry(runOnSystemStartup, pathToExeFile))
				{
					throw new InvalidOperationException("Couldn't  modify Registry.");
				};
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				throw new NotImplementedException();
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				throw new NotImplementedException();
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
				key.DeleteValue(nameof(WalletWasabi));
			}
		}
	}
}
