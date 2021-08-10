using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Helpers
{
	public static class StartupHelper
	{
		public static async Task ModifyStartupSettingAsync(bool runOnSystemStartup)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				WindowsStartupHelper.AddOrRemoveRegistryKey(runOnSystemStartup);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				LinuxStartupHelper.AddOrRemoveDesktopFile(runOnSystemStartup);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				await MacOsStartupHelper.AddOrRemoveLoginItemAsync(runOnSystemStartup).ConfigureAwait(false);
			}
			else
			{
				throw new NotImplementedException("Your operating system is not supported yet.");
			}
		}
	}
}
