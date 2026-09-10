using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Helpers;

public static class WindowsStartupHelper
{
	private const string KeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

	public static void AddOrRemoveRegistryKey(bool runOnSystemStartup)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			throw new InvalidOperationException("Registry modification can only be done on Windows.");
		}

		string pathToExeFile = EnvironmentHelpers.GetExecutablePath();

		string pathToExecWithArgs = $"{pathToExeFile} {StartupHelper.SilentArgument}";

		if (!File.Exists(pathToExeFile))
		{
			throw new InvalidOperationException($"Path: {pathToExeFile} does not exist.");
		}

		using RegistryKey? key = runOnSystemStartup
			? Registry.CurrentUser.CreateSubKey(KeyPath, writable: true)
			: Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);

		if (key is null)
		{
			if (runOnSystemStartup)
			{
				throw new InvalidOperationException("Registry operation failed.");
			}

			return;
		}

		var existingPath = key.GetValue(nameof(WalletWasabi));
		if (existingPath is null && runOnSystemStartup)
		{
			key.SetValue(nameof(WalletWasabi), pathToExecWithArgs);
		}
		else if (existingPath is not null && !runOnSystemStartup)
		{
			key.DeleteValue(nameof(WalletWasabi), false);
		}
	}
}
