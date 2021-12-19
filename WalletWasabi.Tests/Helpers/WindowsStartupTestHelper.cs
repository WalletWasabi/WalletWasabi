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
			RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(PathToRegistyKey, false) ?? throw new InvalidOperationException("Registry operation failed.");
			result = registryKey.GetValueNames().Contains(nameof(WalletWasabi));
		}

		return result;
	}
}
