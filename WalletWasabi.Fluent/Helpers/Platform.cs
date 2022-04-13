using System.Runtime.InteropServices;

namespace WalletWasabi.Fluent.Helpers;

public static class Platform
{
	public static bool Osx => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

	public static bool Linux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

	public static bool Windows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}