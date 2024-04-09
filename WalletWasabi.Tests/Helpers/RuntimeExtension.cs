using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.Helpers;

public static class RuntimeExtension
{
	public static bool IsCurrentlyRunningOsPlatform(this OSPlatform val)
	{
		return RuntimeInformation.IsOSPlatform(val);
	}
}
