using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WalletWasabi.Logging;

namespace WalletWasabi.Helpers
{
	public static class EnvironmentHelpers
	{
		private const int ProcessorCountRefreshIntervalMs = 30000;

		private static volatile int InternalProcessorCount;
		private static volatile int LastProcessorCountRefreshTicks;

		/// <summary>
		/// https://github.com/i3arnon/ConcurrentHashSet/blob/master/src/ConcurrentHashSet/PlatformHelper.cs
		/// </summary>
		internal static int ProcessorCount
		{
			get
			{
				var now = Environment.TickCount;
				if (InternalProcessorCount == 0 || now - LastProcessorCountRefreshTicks >= ProcessorCountRefreshIntervalMs)
				{
					InternalProcessorCount = Environment.ProcessorCount;
					LastProcessorCountRefreshTicks = now;
				}

				return InternalProcessorCount;
			}
		}

		// appName, dataDir
		private static ConcurrentDictionary<string, string> DataDirDict { get; } = new ConcurrentDictionary<string, string>();

		// Do not change the output of this function. Backwards compatibility depends on it.
		public static string GetDataDir(string appName)
		{
			if (DataDirDict.TryGetValue(appName, out string dataDir))
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

		public static string TryGetDefaultBitcoinCoreDataDir()
		{
			string directory = null;

			// https://en.bitcoin.it/wiki/Data_directory
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					var localAppData = Environment.GetEnvironmentVariable("APPDATA");
					if (!string.IsNullOrEmpty(localAppData))
					{
						directory = Path.Combine(localAppData, "Bitcoin");
					}
					else
					{
						throw new DirectoryNotFoundException("Could not find suitable default Bitcoin Core datadir.");
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
						throw new DirectoryNotFoundException("Could not find suitable default Bitcoin Core datadir.");
					}
				}

				return directory;
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex, nameof(EnvironmentHelpers));
			}

			return directory;
		}

		/// <summary>
		/// Executes a command with bash.
		/// https://stackoverflow.com/a/47918132/2061103
		/// </summary>
		/// <param name="cmd"></param>
		public static void ShellExec(string cmd, bool waitForExit = true)
		{
			var escapedArgs = cmd.Replace("\"", "\\\"");

			using (var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = "/bin/sh",
					Arguments = $"-c \"{escapedArgs}\"",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden
				}
			))
			{
				if (waitForExit)
				{
					process.WaitForExit();
					if (process.ExitCode != 0)
					{
						Logger.LogError($"{nameof(ShellExec)} command: {cmd} exited with exit code: {process.ExitCode}, instead of 0.", nameof(EnvironmentHelpers));
					}
				}
			}
		}

		public static bool IsFileTypeAssociated(string fileExtension)
		{
			// Source article: https://edi.wang/post/2019/3/4/read-and-write-windows-registry-in-net-core

			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				throw new InvalidOperationException("Operation only supported on windows.");
			}

			fileExtension = fileExtension.TrimStart('.'); // Remove . if added by the caller.

			using (RegistryKey key = Registry.ClassesRoot.OpenSubKey($".{fileExtension}"))
			{
				if (key != null)
				{
					object val = key.GetValue(null); // Read the (Default) value.
					if (val != null)
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Gets the name of the current method.
		/// </summary>
		public static string GetMethodName([CallerMemberName] string callerName = "")
		{
			return callerName;
		}

		public static string GetFullBaseDirectory()
		{
			var fullBaseDirectory = Path.GetFullPath(AppContext.BaseDirectory);

			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				if (!fullBaseDirectory.StartsWith('/'))
				{
					fullBaseDirectory.Insert(0, "/");
				}
			}

			return fullBaseDirectory;
		}
	}
}
