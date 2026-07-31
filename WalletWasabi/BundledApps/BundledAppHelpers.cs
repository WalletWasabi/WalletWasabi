using System.IO;
using System.Runtime.InteropServices;

namespace WalletWasabi.BundledApps;

public static class BundledAppHelpers
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

	public static string GetBinaryFolder(BundledApp app, OSPlatform? platform = null)
	{
		platform ??= GetCurrentPlatform();

		string fullBaseDirectory = EnvironmentHelpers.GetFullBaseDirectory();
		string commonPartialPath = Path.Combine(fullBaseDirectory, "BundledApps", "Binaries");

		string path;
		if (platform == OSPlatform.Windows)
		{
			path = Path.Combine(commonPartialPath, "win-x64");
		}
		else if (platform == OSPlatform.Linux)
		{
			path = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
				? Path.Combine(commonPartialPath, "linux-arm64")
				: Path.Combine(commonPartialPath, "linux-x64");
		}
		else if (platform == OSPlatform.OSX)
		{
			if (app == BundledApp.Tor)
			{
				// Tor uses universal binaries on macOS, so we can use the same binary for both Intel and Apple Silicon.
				path = Path.Combine(commonPartialPath, "osx64");
			}
			else
			{
				path = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
					? Path.Combine(commonPartialPath, "osx-arm64")
					: Path.Combine(commonPartialPath, "osx64");
			}
		}
		else
		{
			throw new NotSupportedException("Operating system is not supported.");
		}

		return path;
	}

	public static string GetBinaryPath(BundledApp app)
	{
		var platform = GetCurrentPlatform();
		var binaryFolder = GetBinaryFolder(app, platform);

		var binaryNameWithoutExtension = app switch
		{
			BundledApp.Tor => "tor",
			BundledApp.Hwi => "hwi",
			BundledApp.Bitcoind => "bitcoind",
			_ => throw new NotSupportedException($"Bundled app '{app}' is not supported.")
		};

		string fileName = GetFilenameWithExtension(binaryNameWithoutExtension, platform);

		return Path.Combine(binaryFolder, fileName);
	}

	public static string GetFilenameWithExtension(string binaryNameWithoutExtension, OSPlatform? platform = null)
	{
		platform ??= GetCurrentPlatform();
		return platform.Value == OSPlatform.Windows ? $"{binaryNameWithoutExtension}.exe" : $"{binaryNameWithoutExtension}";
	}
}
