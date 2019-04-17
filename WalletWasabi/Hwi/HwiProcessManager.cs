using NBitcoin;
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
		public static Network Network { get; private set; }

		static HwiProcessManager()
		{
			AsyncLock = new AsyncLock();
			Network = Network.Main;
		}

		public static async Task<PSBT> SignTxAsync(HardwareWalletInfo hardwareWalletInfo, PSBT psbt)
		{
			var psbtString = psbt.ToBase64();
			var networkString = Network == Network.Main ? "" : "--testnet";
			JToken jtok = await SendCommandAsync($"{networkString} --device-type \"{hardwareWalletInfo.Type.ToString().ToLowerInvariant()}\" --device-path \"{hardwareWalletInfo.Path}\" signtx {psbtString}");
			JObject json = jtok as JObject;
			var signedPsbtString = json.Value<string>("psbt");
			var signedPsbt = PSBT.Parse(signedPsbtString, Network);
			signedPsbt.Finalize();

			return signedPsbt;
		}

		/// <summary>
		/// Acquire bech32 xpub on path: m/84h/0h/0h
		/// https://github.com/bitcoin-core/HWI/blob/master/docs/examples.md
		/// </summary>
		public static async Task<ExtPubKey> GetXpubAsync(HardwareWalletInfo hardwareWalletInfo)
		{
			var networkString = Network == Network.Main ? "" : "--testnet ";
			JToken jtok = await SendCommandAsync($"{networkString}--device-type \"{hardwareWalletInfo.Type.ToString().ToLowerInvariant()}\" --device-path \"{hardwareWalletInfo.Path}\" getxpub m/84h/0h/0h");
			JObject json = jtok as JObject;
			string xpub = json.Value<string>("xpub");

			ExtPubKey extpub = ExtPubKey.Parse(xpub);

			return extpub;
		}

		public static async Task<IEnumerable<HardwareWalletInfo>> EnumerateAsync()
		{
			JToken jtok = await SendCommandAsync("enumerate");
			JArray jarr = jtok as JArray;
			var hwis = new List<HardwareWalletInfo>();
			foreach (JObject json in jarr)
			{
				var fingerprint = json.Value<string>("fingerprint");
				var serialNumber = json.Value<string>("serial_number");
				var typeString = json.Value<string>("type");
				var path = json.Value<string>("path");
				var error = json.Value<string>("error");

				var type = (HardwareWalletType)Enum.Parse(typeof(HardwareWalletType), typeString, ignoreCase: true);

				var hwi = new HardwareWalletInfo(fingerprint, serialNumber, type, path, error);
				hwis.Add(hwi);
			}

			return hwis;
		}

		public static async Task<JToken> SendCommandAsync(string command)
		{
			using (await AsyncLock.LockAsync())
			{
				if (!File.Exists(HwiPath))
				{
					var exeName = Path.GetFileName(HwiPath);
					throw new FileNotFoundException($"{exeName} not found at {HwiPath}. Maybe it was removed by antivirus software!");
				}

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
					await process.WaitForExitAsync();
					if (process.ExitCode != 0)
					{
						throw new IOException($"Command: {command} exited with exit code: {process.ExitCode}, instead of 0.");
					}

					string response = await process.StandardOutput.ReadToEndAsync();
					var jToken = JToken.Parse(response);
					if (jToken is JObject json)
					{
						if (json.TryGetValue("error", out JToken err))
						{
							var errString = err.ToString();
							throw new IOException(errString);
						}
					}

					return jToken;

					//if (command == "enumerate")
					//{
					//	//string response = "[{\"fingerprint\": \"8038ecd9\", \"serial_number\": \"205A32753042\", \"type\": \"coldcard\", \"path\": \"0001:0005:00\"},{\"fingerprint\": \"8338ecd9\", \"serial_number\": \"205A32753042\", \"type\": \"keepkey\", \"path\": \"0001:0005:00\"},{\"fingerprint\": \"8038ecd2\", \"serial_number\": \"205A32753042\", \"type\": \"coldcard\", \"path\": \"0001:0005:00\"}]";
					//	string response = "[{\"fingerprint\": \"8038ecd9\", \"serial_number\": \"205A32753042\", \"type\": \"coldcard\", \"path\": \"0001:0005:00\"},{\"fingerprint\": \"8338ecd9\", \"serial_number\": \"205A32753042\", \"type\": \"keepkey\", \"path\": \"0001:0005:00\"}]";
					//	var jToken = JToken.Parse(response);
					//	return jToken;
					//}
					//if (command.Contains("getxpub", StringComparison.OrdinalIgnoreCase))
					//{
					//	string response = "{\"xpub\": \"xpub6DP9afdc7qsz7s7mwAvciAR2dV6vPC3gyiQbqKDzDcPAq3UQChKPimHc3uCYfTTkpoXdwRTFnVTBdFpM9ysbf6KV34uMqkD3zXr6FzkJtcB\"}";
					//	var jToken = JToken.Parse(response);
					//	return jToken;
					//}
					//else
					//{
					//	string response = await process.StandardOutput.ReadToEndAsync();
					//	var jToken = JToken.Parse(response);
					//	string err = null;
					//	try
					//	{
					//		JObject json = jToken as JObject;
					//		err = json.Value<string>("error");
					//	}
					//	catch (Exception ex)
					//	{
					//		Logger.LogTrace(ex, nameof(HwiProcessManager));
					//	}
					//	if (err != null)
					//	{
					//		throw new IOException(err);
					//	}

					//	return jToken;
					//}
				}
			}
		}

		public static async Task EnsureHwiInstalledAsync(string dataDir, Network network, bool logFound = true)
		{
			Network = network;

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
			else if (!IoHelpers.CheckExpectedHash(hwiPath, Path.Combine(fullBaseDirectory, "Hwi", "Software")))
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
				if (logFound)
				{
					Logger.LogInfo($"HWI instance found at {hwiPath}.", nameof(HwiProcessManager));
				}
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
				string chmodHwiDirCmd = $"chmod -R 750 {hwiDir}";
				EnvironmentHelpers.ShellExec(chmodHwiDirCmd);
				Logger.LogInfo($"Shell command executed: {chmodHwiDirCmd}.", nameof(HwiProcessManager));
			}
		}
	}
}
