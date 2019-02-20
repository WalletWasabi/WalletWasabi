using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using WalletWasabi.Logging;

namespace WalletWasabi.Helpers
{
	public static class EnvironmentHelpers
	{
		private const int ProcessorCountRefreshIntervalMs = 30000;

		private static volatile int _processorCount;
		private static volatile int _lastProcessorCountRefreshTicks;

		/// <summary>
		/// https://github.com/i3arnon/ConcurrentHashSet/blob/master/src/ConcurrentHashSet/PlatformHelper.cs
		/// </summary>
		internal static int ProcessorCount
		{
			get
			{
				var now = Environment.TickCount;
				if (_processorCount == 0 || now - _lastProcessorCountRefreshTicks >= ProcessorCountRefreshIntervalMs)
				{
					_processorCount = Environment.ProcessorCount;
					_lastProcessorCountRefreshTicks = now;
				}

				return _processorCount;
			}
		}

		public static string GetDataDir(string appName)
		{
			string directory = null;

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

			if (Directory.Exists(directory)) return directory;

			Logger.LogInfo($"Creating data directory at `{directory}`.");
			Directory.CreateDirectory(directory);

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
						if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
						{
							directory = Path.Combine(home, "Library", "Application Support", "Bitcoin");
						}
						else // Linux
						{
							directory = Path.Combine(home, ".bitcoin");
						}
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
	}
}
