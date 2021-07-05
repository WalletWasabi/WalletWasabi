using Microsoft.Win32;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Helpers
{
	public static class StartupHelper
	{
		private const string KeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

		public static bool TryModifyStartupSetting(bool runOnSystemStartup)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				Assembly assembly = Assembly.GetEntryAssembly() ?? throw new NullReferenceException();
				string pathToExeFile = assembly.Location[..^4] + ".exe";
				if (File.Exists(pathToExeFile))
				{
					return TryModifyRegistry(runOnSystemStartup, pathToExeFile);
				}
				else
				{
					Logger.LogError($"Path {pathToExeFile} does not exist.");
				}
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
					key.SetValue(nameof(WalletWasabi), pathToExeFile);
				}
				else
				{
					key.DeleteValue(nameof(WalletWasabi));
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
