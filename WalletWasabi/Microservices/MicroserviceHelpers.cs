using System.IO;
using System.Runtime.InteropServices;
using WalletWasabi.Helpers;

namespace WalletWasabi.Microservices;

public static class MicroserviceHelpers
{
	public static OSPlatform GetCurrentPlatform()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return OSPlatform.Windows;
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return OSPlatform.Linux;
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			return OSPlatform.OSX;
		}
		else
		{
			throw new NotSupportedException("Platform is not supported.");
		}
	}

	public static string GetBinaryFolder(OSPlatform? platform = null)
	{
		platform ??= GetCurrentPlatform();

		string fullBaseDirectory = EnvironmentHelpers.GetFullBaseDirectory();
		string commonPartialPath = Path.Combine(fullBaseDirectory, "Microservices", "Binaries");

		string path;
		if (platform == OSPlatform.Windows)
		{
			path = Path.Combine(commonPartialPath, "win64");
		}
		else if (platform == OSPlatform.Linux)
		{
			path = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
				? Path.Combine(commonPartialPath, "linux_arm64")
				: Path.Combine(commonPartialPath, "lin64");
		}
		else if (platform == OSPlatform.OSX)
		{
			path = Path.Combine(commonPartialPath, "osx64");
		}
		else
		{
			throw new NotSupportedException("Operating system is not supported.");
		}

		return path;
	}

	public static string GetBinaryPath(string binaryNameWithoutExtension, OSPlatform? platform = null)
	{
		platform ??= GetCurrentPlatform();
		string binaryFolder = GetBinaryFolder(platform);
		string fileName = GetFilenameWithExtension(binaryNameWithoutExtension, platform);

		return Path.Combine(binaryFolder, fileName);
	}

	public static string GetFilenameWithExtension(string binaryNameWithoutExtension, OSPlatform? platform = null)
	{
        platform ??= GetCurrentPlatform();
        return platform.Value == OSPlatform.Windows ? $"{binaryNameWithoutExtension}.exe" : $"{binaryNameWithoutExtension}";
    }
}
