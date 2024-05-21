using Microsoft.Win32;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.Helpers;

public static class EnvironmentHelpers
{
	// appName, dataDir
	private static ConcurrentDictionary<string, string> DataDirDict { get; } = new ConcurrentDictionary<string, string>();

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
				Logger.LogInfo($"Using HOME environment variable for initializing application data at `{directory}`.");
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
				Logger.LogInfo($"Using APPDATA environment variable for initializing application data at `{directory}`.");
			}
			else
			{
				throw new DirectoryNotFoundException("Could not find suitable datadir.");
			}
		}

		if (Directory.Exists(directory))
		{
			DataDirDict.TryAdd(appName, directory);
			return directory;
		}

		Logger.LogInfo($"Creating data directory at `{directory}`.");
		Directory.CreateDirectory(directory);

		DataDirDict.TryAdd(appName, directory);
		return directory;
	}

	/// <summary>
	/// Gets Bitcoin <c>datadir</c> parameter from:
	/// <list type="bullet">
	/// <item><c>APPDATA</c> environment variable on Windows, and</item>
	/// <item><c>HOME</c> environment variable on other platforms.</item>
	/// </list>
	/// </summary>
	/// <returns><c>datadir</c> or empty string.</returns>
	/// <seealso href="https://en.bitcoin.it/wiki/Data_directory"/>
	public static string GetDefaultBitcoinCoreDataDirOrEmptyString()
	{
		string directory = "";

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var localAppData = Environment.GetEnvironmentVariable("APPDATA");
			if (!string.IsNullOrEmpty(localAppData))
			{
				directory = Path.Combine(localAppData, "Bitcoin");
			}
			else
			{
				Logger.LogDebug($"Could not find suitable default {Constants.BuiltinBitcoinNodeName} datadir.");
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
			else
			{
				Logger.LogDebug($"Could not find suitable default {Constants.BuiltinBitcoinNodeName} datadir.");
			}
		}

		return directory;
	}

	// This method removes the path and file extension.
	/// <summary>
	/// Executes a command with Bourne shell.
	/// https://stackoverflow.com/a/47918132/2061103
	/// </summary>
	public static async Task ShellExecAsync(string cmd, bool waitForExit = true)
		=> await ShellExecAndGetResultAsync(cmd, waitForExit, false).ConfigureAwait(false);

	public static async Task<string> ShellExecAndGetResultAsync(string cmd)
		=> await ShellExecAndGetResultAsync(cmd, true, true).ConfigureAwait(false);

	/// <summary>
	/// Executes a command with Bourne shell and returns Standard Output.
	/// </summary>
	private static async Task<string> ShellExecAndGetResultAsync(string cmd, bool waitForExit = true, bool readResult = false)
	{
		var escapedArgs = cmd.Replace("\"", "\\\"");

		var startInfo = new ProcessStartInfo
		{
			FileName = "/usr/bin/env",
			Arguments = $"sh -c \"{escapedArgs}\"",
			RedirectStandardOutput = true,
			RedirectStandardError = readResult,
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden
		};

		if (readResult)
		{
			waitForExit = true;
		}
		string output = "";

		if (waitForExit)
		{
			using var process = new ProcessAsync(startInfo);
			process.Start();

			if (readResult)
			{
				output = process.StandardOutput.ReadToEnd();
			}

			await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
			if (process.ExitCode != 0)
			{
				Logger.LogError($"{nameof(ShellExecAsync)} command: {cmd} exited with exit code: {process.ExitCode}, instead of 0.");
			}
		}
		else
		{
			using var process = Process.Start(startInfo);
		}

		return output;
	}

	public static bool IsFileTypeAssociated(string fileExtension)
	{
		// Source article: https://edi.wang/post/2019/3/4/read-and-write-windows-registry-in-net-core

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			throw new InvalidOperationException("Operation only supported on windows.");
		}

		fileExtension = fileExtension.TrimStart('.'); // Remove . if added by the caller.

		using var key = Registry.ClassesRoot.OpenSubKey($".{fileExtension}");

		// Read the (Default) value.
		return key?.GetValue(null) is not null;
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
}
