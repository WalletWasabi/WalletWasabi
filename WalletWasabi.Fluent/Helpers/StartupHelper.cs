using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Helpers;

public static class StartupHelper
{
	public const string SilentArgument = "startsilent";

	public static async Task ModifyStartupSettingAsync(bool runOnSystemStartup)
	{
		try
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				WindowsStartupHelper.AddOrRemoveRegistryKey(runOnSystemStartup);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				await LinuxStartupHelper.AddOrRemoveDesktopFileAsync(runOnSystemStartup).ConfigureAwait(false);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				await MacOsStartupHelper.AddOrRemoveStartupItemAsync(runOnSystemStartup).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			// Suppress exception to avoid potential crashes.
			Logger.LogError($"{ex}");
		}
	}
}
