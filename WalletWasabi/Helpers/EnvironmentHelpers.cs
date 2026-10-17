using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WalletWasabi.Helpers;

public static class EnvironmentHelpers
{
	// appName, dataDir
	private static ConcurrentDictionary<string, string> DataDirDict { get; } = new();

	// Do not change the output of this function. Backwards compatibility depends on it.
	public static string GetDataDir(string appName)
	{
		if (DataDirDict.TryGetValue(appName, out string? dataDir))
		{
			return dataDir;
		}

		string directory;

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var home = Environment.GetEnvironmentVariable("HOME");
			if (!string.IsNullOrEmpty(home))
			{
				directory = Path.Combine(home, "." + appName.ToLowerInvariant());
			}
			else
			{
				throw new DirectoryNotFoundException("Could not find suitable datadir.");
			}
		}
		else
		{
			var localAppData = Environment.GetEnvironmentVariable("APPDATA");
			if (!string.IsNullOrEmpty(localAppData))
			{
				directory = Path.Combine(localAppData, appName);
			}
			else
			{
				throw new DirectoryNotFoundException("Could not find suitable datadir.");
			}
		}

		CreateOwnerOnlyDirectory(directory);

		DataDirDict.TryAdd(appName, directory);
		return directory;
	}

	/// <summary>
	/// Creates a directory that only its owner can enter (mode 0700 on Unix). An existing directory is left as it is,
	/// see <see cref="WarnIfWorldAccessible"/>.
	/// </summary>
	public static void CreateOwnerOnlyDirectory(string directory)
	{
		if (OperatingSystem.IsWindows())
		{
			Directory.CreateDirectory(directory);
		}
		else
		{
			Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
	}

	/// <summary>Logs a warning when other users of the machine can access the directory. Nothing is checked on Windows.</summary>
	/// <returns>Whether a warning was logged.</returns>
	public static bool WarnIfWorldAccessible(string directory)
	{
		const UnixFileMode WorldAccess = UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

		if (OperatingSystem.IsWindows() || (File.GetUnixFileMode(directory) & WorldAccess) == UnixFileMode.None)
		{
			return false;
		}

		Logger.LogWarning($"Directory '{directory}' is accessible to all users of this machine. Restrict it, for example with: chmod o-rwx '{directory}'");
		return true;
	}


	// This method removes the path and file extension.
	//
	// Given Wasabi releases are currently built using Windows, the generated assemblies contain
	// the hard coded "C:\Users\User\Desktop\WalletWasabi\.......\FileName.cs" string because that
	// is the real path of the file, it doesn't matter what OS was targeted.
	// In Windows and Linux that string is a valid path and that means Path.GetFileNameWithoutExtension
	// can extract the file name but in the case of OSX the same string is not a valid path so, it assumes
	// the whole string is the file name.
	public static string ExtractFileName(string callerFilePath)
	{
		var lastSeparatorIndex = callerFilePath.LastIndexOf('\\');
		if (lastSeparatorIndex == -1)
		{
			lastSeparatorIndex = callerFilePath.LastIndexOf('/');
		}

		var fileName = callerFilePath;

		if (lastSeparatorIndex != -1)
		{
			lastSeparatorIndex++;
			fileName = callerFilePath[lastSeparatorIndex..]; // From lastSeparatorIndex until the end of the string.
		}

		var fileNameWithoutExtension = fileName.TrimEnd(".cs", StringComparison.InvariantCultureIgnoreCase);
		return fileNameWithoutExtension;
	}

	public static string GetFullBaseDirectory()
	{
		var fullBaseDirectory = Path.GetFullPath(AppContext.BaseDirectory);

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			if (!fullBaseDirectory.StartsWith('/'))
			{
				fullBaseDirectory = fullBaseDirectory.Insert(0, "/");
			}
		}

		return fullBaseDirectory;
	}

	public static string GetExecutablePath()
	{
		var fullBaseDir = GetFullBaseDirectory();
		var wassabeeFileName = Path.Combine(fullBaseDir, Constants.ExecutableName);
		wassabeeFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{wassabeeFileName}.exe" : $"{wassabeeFileName}";
		if (File.Exists(wassabeeFileName))
		{
			return wassabeeFileName;
		}
		var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? throw new NullReferenceException("Assembly or Assembly's Name was null.");
		var fluentExecutable = Path.Combine(fullBaseDir, assemblyName);
		return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{fluentExecutable}.exe" : $"{fluentExecutable}";
	}

	public static string GetDefaultBitcoinDataDir()
	{
		string directory = "";

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var localAppData = Environment.GetEnvironmentVariable("APPDATA");
			if (!string.IsNullOrEmpty(localAppData))
			{
				directory = Path.Combine(localAppData, "Bitcoin");
			}
		}
		else
		{
			var home = Environment.GetEnvironmentVariable("HOME");
			if (!string.IsNullOrEmpty(home))
			{
				directory = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
					? Path.Combine(home, "Library", "Application Support", "Bitcoin")
					: Path.Combine(home, ".bitcoin"); // Linux
			}
		}

		return directory;
	}
}

public enum OS
{
	Windows,
	Linux,
	OSX,
	FreeBSD
}

public static class PlatformInformation
{
	public static OS GetOsPlatform()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return OS.Windows;
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			return OS.OSX;
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return OS.Linux;
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
		{
			return OS.FreeBSD;
		}

		throw new NotSupportedException("Unknown OS platform.");
	}


	public static bool IsDebianBasedOS()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return false;
		}

		if (!File.Exists("/etc/os-release"))
		{
			return false;
		}

		var osReleaseContent = File.ReadAllText("/etc/os-release");

		// Check for Debian as base
		if (osReleaseContent.Contains("ID_LIKE=debian") ||
			osReleaseContent.Contains("ID=debian"))
		{
			return true;
		}

		// Check for known Debian derivatives
		var debianDerivatives = new[] {"ubuntu", "linuxmint", "elementary", "pop", "zorin"};
		foreach (var derivative in debianDerivatives)
		{
			if (osReleaseContent.Contains($"ID={derivative}") ||
				osReleaseContent.Contains($"ID_LIKE=") && osReleaseContent.Contains(derivative))
			{
				return true;
			}
		}

		return false;
	}

	private static bool TryGetLinuxOSRelease([NotNullWhen(true)] out string? content)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists("/etc/os-release"))
		{
			try
			{
				content = File.ReadAllText("/etc/os-release");
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogDebug($"Failed to read /etc/os-release for operating system detection: {ex.Message}");
			}
		}

		content = null;
		return false;
	}

	public static bool IsTailsOS() =>
		TryGetLinuxOSRelease(out var osReleaseContent) && (
			osReleaseContent.Contains("ID=tails", StringComparison.OrdinalIgnoreCase) ||
			osReleaseContent.Contains("NAME=\"Tails\"", StringComparison.OrdinalIgnoreCase));

	public static bool IsWhonix() =>
		TryGetLinuxOSRelease(out var osReleaseContent) && (
			osReleaseContent.Contains("ID=whonix", StringComparison.OrdinalIgnoreCase) ||
			osReleaseContent.Contains("NAME=\"Whonix\"", StringComparison.OrdinalIgnoreCase) ||
			osReleaseContent.Contains("ID_LIKE=debian whonix", StringComparison.OrdinalIgnoreCase));
}
