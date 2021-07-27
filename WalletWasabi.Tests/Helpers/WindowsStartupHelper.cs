using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.Helpers
{
	public class WindowsStartupHelper
	{
		private const string PathToRegistyKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

		public bool RegistryKeyExists()
		{
			bool result = false;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(PathToRegistyKey, false);
				result = registryKey.GetValueNames().Contains(nameof(WalletWasabi));
			}

			return result;
		}
	}
}
