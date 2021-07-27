using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Helpers
{
	public static class StartupHelper
	{
		public static async Task ModifyStartupSettingAsync(bool runOnSystemStartup)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				string pathToExeFile = EnvironmentHelpers.GetExecutablePath();
				if (!File.Exists(pathToExeFile))
				{
					throw new InvalidOperationException($"Path {pathToExeFile} does not exist.");
				}
				WindowsStartupHelper.StartOnWindowsStartup(runOnSystemStartup, pathToExeFile);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				await LinuxStartupHelper.StartOnLinuxStartupAsync(runOnSystemStartup);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				await MacOsStartupHelper.ModifyLoginItemsAsync(runOnSystemStartup);
			}
		}
	}
}
