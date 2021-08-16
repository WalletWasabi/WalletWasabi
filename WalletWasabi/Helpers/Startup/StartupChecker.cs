using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Helpers
{
	public static class StartupChecker
	{
		public static async Task<bool> GetCurrentValueAsync(CancellationToken cancel = default)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return WindowsStartupChecker.CheckRegistryKeyExists();
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return await LinuxStartupChecker.CheckDesktopFileAsync().ConfigureAwait(false);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return await MacOsStartupChecker.CheckLoginItemExistsAsync().ConfigureAwait(false);
			}
			else
			{
				throw new NotImplementedException("Your platform is not supported yet.");
			}
		}
	}
}
