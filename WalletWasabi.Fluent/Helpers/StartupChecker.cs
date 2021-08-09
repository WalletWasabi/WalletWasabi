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
		public static bool Validate()
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
				result = MacOsStartupHelper.CheckLoginItemExists();
			}

			return result;
		}
	}
}
