using Microsoft.Win32;
using System.Linq;
using System.Runtime.InteropServices;

namespace WalletWasabi.Tests.Helpers;

public class WindowsStartupTestHelper
{
	private const string PathToRegistyKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

	public bool RegistryKeyExists()
	{
		bool result = false;

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			using RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(PathToRegistyKey, false);
			result = registryKey?.GetValueNames().Contains(nameof(WalletWasabi)) is true;
		}

		return result;
	}
}
