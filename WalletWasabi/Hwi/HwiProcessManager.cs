using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Hwi
{
	/// <summary>
	/// https://github.com/bitcoin-core/HWI
	/// </summary>
	public static class HwiProcessManager
	{
		public static string HwiPath { get; private set; }
		public static AsyncLock AsyncLock { get; }

		static HwiProcessManager()
		{
			AsyncLock = new AsyncLock();
		}

		public static async Task<IEnumerable<HardwareWalletInfo>> EnumerateAsync()
		{
			JArray jarr = await SendCommandAsync("enumerate");
			var hwis = new List<HardwareWalletInfo>();
			foreach (JObject json in jarr)
			{
				var fingerprint = json.Value<string>("fingerprint");
				var serialNumber = json.Value<string>("serialNumber");
				var path = json.Value<string>("path");
				var typeString = json.Value<string>("type");

				var type = (HardwareWalletType)Enum.Parse(typeof(HardwareWalletType), typeString, ignoreCase: true);

				var hwi = new HardwareWalletInfo(fingerprint, serialNumber, type, path);
				hwis.Add(hwi);
			}

			return hwis;
		}

		public static async Task<JArray> SendCommandAsync(string command)
		{
			using (await AsyncLock.LockAsync())
			using (var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = HwiPath,
					Arguments = command,
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden
				}
			))
			{
				process.WaitForExit();
				if (process.ExitCode != 0)
				{
					throw new IOException($"Command: {command} exited with exit code: {process.ExitCode}, instead of 0.");
				}

				//string response = "[{\"fingerprint\": \"8038ecd9\", \"serial_number\": \"205A32753042\", \"type\": \"coldcard\", \"path\": \"0001:0005:00\"},{\"fingerprint\": \"8338ecd9\", \"serial_number\": \"205A32753042\", \"type\": \"keepkey\", \"path\": \"0001:0005:00\"},{\"fingerprint\": \"8038ecd2\", \"serial_number\": \"205A32753042\", \"type\": \"coldcard\", \"path\": \"0001:0005:00\"}]";
				//string response = "[{\"fingerprint\": \"8038ecd9\", \"serial_number\": \"205A32753042\", \"type\": \"coldcard\", \"path\": \"0001:0005:00\"},{\"fingerprint\": \"8338ecd9\", \"serial_number\": \"205A32753042\", \"type\": \"keepkey\", \"path\": \"0001:0005:00\"}]";
				string response = await process.StandardOutput.ReadToEndAsync();
				var jarr = JArray.Parse(response);
				return jarr;
			}
		}

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

			HwiPath = hwiPath;
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
