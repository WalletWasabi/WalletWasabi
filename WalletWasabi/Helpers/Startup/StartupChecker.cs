using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Helpers
{
	public static class StartupChecker
	{
		public static async Task<bool> GetCurrentValueAsync(CancellationToken cancel = default)
		{
			bool result = false;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				result = WindowsStartupChecker.CheckRegistryKeyExists();
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				result = await LinuxStartupChecker.CheckDesktopFileAsync().ConfigureAwait(false);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				result = await MacOsStartupChecker.CheckLoginItemExistsAsync().ConfigureAwait(false);
			}
			else
			{
				throw new NotImplementedException("Your platform is not supported yet.");
			}

			return result;
		}
	}
}
