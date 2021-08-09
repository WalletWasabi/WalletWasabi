using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Helpers
{
	public static class StartupChecker
	{
		public async static Task<bool> ValidateAsync()
		{
			bool result = false;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				result = WindowsStartupHelper.CheckRegistryKeyExists();
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				result = LinuxStartupHelper.CheckDesktopFile();
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				result = await MacOsStartupHelper.CheckLoginItemExistsAsync().ConfigureAwait(false);
			}

			return result;
		}
	}
}
