using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WalletWasabi.Fluent.Helpers
{
	public static class Platform
	{
		public static bool IsOSX { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

		public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

		public static bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
	}
}
