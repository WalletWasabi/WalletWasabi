using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Hwi
{
	public static class HwiProcessManager
	{
		public static async Task EnsureHwiInstalledAsync(string dataDir)
		{
			var fullBaseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				if (!fullBaseDirectory.StartsWith('/'))
				{
					fullBaseDirectory.Insert(0, "/");
				}
			}

			var hwiDir = Path.Combine(dataDir, "hwi");

			string hwiPath;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				hwiPath = $@"{hwiDir}\hwi.exe";
			}
			else // Linux or OSX
			{
				hwiPath = $@"{hwiDir}/hwi";
			}

			if (!File.Exists(hwiPath))
			{
				Logger.LogInfo($"HWI instance NOT found at {hwiPath}. Attempting to acquire it...", nameof(HwiProcessManager));
				await InstallHwiAsync(fullBaseDirectory, hwiDir);
			}
			else if (new FileInfo(hwiPath).CreationTimeUtc < new DateTime(2019, 04, 13, 0, 0, 0, 0, DateTimeKind.Utc))
			{
				Logger.LogInfo($"Updating HWI...", nameof(HwiProcessManager));

				string backupHwiDir = $"{hwiDir}_backup";
				if (Directory.Exists(backupHwiDir))
				{
					Directory.Delete(backupHwiDir, true);
				}
				Directory.Move(hwiDir, backupHwiDir);

				await InstallHwiAsync(fullBaseDirectory, hwiDir);
			}
			else
			{
				Logger.LogInfo($"HWI instance found at {hwiPath}.", nameof(HwiProcessManager));
			}
		}

		private static async Task InstallHwiAsync(string fullBaseDirectory, string hwiDir)
		{
			string hwiSoftwareDir = Path.Combine(fullBaseDirectory, "Hwi", "Software");

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				string hwiWinZip = Path.Combine(hwiSoftwareDir, "hwi-win64.zip");
				await IoHelpers.BetterExtractZipToDirectoryAsync(hwiWinZip, hwiDir);
				Logger.LogInfo($"Extracted {hwiWinZip} to {hwiDir}.", nameof(HwiProcessManager));
			}
			else // Linux or OSX
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					string hwiLinuxZip = Path.Combine(hwiSoftwareDir, "hwi-linux64.zip");
					await IoHelpers.BetterExtractZipToDirectoryAsync(hwiLinuxZip, hwiDir);
					Logger.LogInfo($"Extracted {hwiLinuxZip} to {hwiDir}.", nameof(HwiProcessManager));
				}
				else // OSX
				{
					string hwiOsxZip = Path.Combine(hwiSoftwareDir, "hwi-osx64.zip");
					await IoHelpers.BetterExtractZipToDirectoryAsync(hwiOsxZip, hwiDir);
					Logger.LogInfo($"Extracted {hwiOsxZip} to {hwiDir}.", nameof(HwiProcessManager));
				}

				// Make sure there's sufficient permission.
				string chmodHwiDirCmd = $"chmod -R 777 {hwiDir}";
				EnvironmentHelpers.ShellExec(chmodHwiDirCmd);
				Logger.LogInfo($"Shell command executed: {chmodHwiDirCmd}.", nameof(HwiProcessManager));
			}
		}
	}
}
