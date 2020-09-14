using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Tor
{
	/// <summary>
	/// Installs Tor from <c>data-folder.zip</c> and <c>tor-PLATFORM.zip</c> files which are part of Wasabi Wallet distribution.
	/// </summary>
	public class TorInstallator
	{
		/// <summary>
		/// Creates new instance.
		/// </summary>
		public TorInstallator(TorSettings settings)
		{
			Settings = settings;
		}

		/// <summary>Tor settings containing all necessary settings for Tor installation and running.</summary>
		public TorSettings Settings { get; }

		/// <summary>
		/// Installs Tor for Wasabi Wallet use.
		/// </summary>
		/// <returns><see cref="Task"/> instance.</returns>
		public async Task<bool> InstallAsync()
		{
			// Folder where to install Tor to.
			string destinationFolder = Settings.TorDir;

			try
			{
				string dataZipPath = Path.Combine(Settings.DistributionFolder, "data-folder.zip");
				await IoHelpers.BetterExtractZipToDirectoryAsync(dataZipPath, destinationFolder).ConfigureAwait(false);
				Logger.LogInfo($"Extracted '{dataZipPath}' to '{destinationFolder}'.");

				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					string zipPath = Path.Combine(Settings.DistributionFolder, "tor-win64.zip");
					await IoHelpers.BetterExtractZipToDirectoryAsync(zipPath, destinationFolder).ConfigureAwait(false);
					Logger.LogInfo($"Extracted '{zipPath}' to '{destinationFolder}'.");
				}
				else // Linux or macOS
				{
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					{
						string zipPath = Path.Combine(Settings.DistributionFolder, "tor-linux64.zip");
						await IoHelpers.BetterExtractZipToDirectoryAsync(zipPath, destinationFolder).ConfigureAwait(false);
						Logger.LogInfo($"Extracted '{zipPath}' to '{destinationFolder}'.");
					}
					else // OSX
					{
						string zipPath = Path.Combine(Settings.DistributionFolder, "tor-osx64.zip");
						await IoHelpers.BetterExtractZipToDirectoryAsync(zipPath, destinationFolder).ConfigureAwait(false);
						Logger.LogInfo($"Extracted '{zipPath}' to '{destinationFolder}'.");
					}

					// Make sure there's sufficient permission.
					string chmodTorDirCmd = $"chmod -R 750 {destinationFolder}";
					await EnvironmentHelpers.ShellExecAsync(chmodTorDirCmd, waitForExit: true).ConfigureAwait(false);
					Logger.LogInfo($"Shell command executed: '{chmodTorDirCmd}'.");
				}

				bool verification = File.Exists(Settings.TorPath);

				Logger.LogDebug($"Tor installation finished. Installed correctly? {verification}.");
				return verification;
			}
			catch (Exception e)
			{
				Logger.LogError("Tor installation failed.", e);
			}

			return false;
		}

		/// <summary>
		/// Verify that Tor is installed and checksums of installed binaries are correct.
		/// </summary>
		/// <returns><see cref="Task"/> instance.</returns>
		public async Task<bool> VerifyInstallationAsync()
		{
			if (!File.Exists(Settings.TorPath))
			{
				Logger.LogInfo($"Tor instance NOT found at '{Settings.TorPath}'. Attempting to acquire it.");
				return await InstallAsync().ConfigureAwait(false);
			}
			else if (!IoHelpers.CheckExpectedHash(Settings.HashSourcePath, Settings.DistributionFolder))
			{
				Logger.LogInfo("Install the latest Tor version.");
				string backupDir = $"{Settings.TorDir}_backup";

				if (Directory.Exists(backupDir))
				{
					Logger.LogInfo($"Delete backup directory '{backupDir}'.");
					Directory.Delete(backupDir, true);
				}

				Directory.Move(Settings.TorDir, backupDir);
				return await InstallAsync().ConfigureAwait(false);
			}
			else
			{
				Logger.LogInfo($"Tor instance found at '{Settings.TorPath}'.");
				return true;
			}
		}
	}
}
