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
		/// Creates a new instance.
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
		/// <returns>Returns <c>true</c> if <see cref="TorSettings.TorBinaryFilePath"/> is present after installation, <c>false</c> otherwise.</returns>
		public async Task<bool> InstallAsync()
		{
			try
			{
				// Common for all platforms.
				await ExtractZipFileAsync(Path.Combine(Settings.DistributionFolder, "data-folder.zip"), Settings.TorDir).ConfigureAwait(false);

				// File differs per platform.
				await ExtractZipFileAsync(Path.Combine(Settings.DistributionFolder, $"tor-{GetPlatformIdentifier()}.zip"), Settings.TorDir).ConfigureAwait(false);

				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					// Set sufficient file permission.
					string shellCommand = $"chmod -R 750 {Settings.TorDir}";
					await EnvironmentHelpers.ShellExecAsync(shellCommand, waitForExit: true).ConfigureAwait(false);
					Logger.LogInfo($"Shell command executed: '{shellCommand}'.");
				}

				bool verification = File.Exists(Settings.TorBinaryFilePath);

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
		/// Verifies that Tor is installed and checksums of installed binaries are correct.
		/// </summary>
		/// <returns>Returns <c>true</c> if <see cref="TorSettings.TorBinaryFilePath"/> is present, <c>false</c> otherwise.</returns>
		public async Task<bool> VerifyInstallationAsync()
		{
			try
			{
				if (!File.Exists(Settings.TorBinaryFilePath))
				{
					Logger.LogInfo($"Tor instance NOT found at '{Settings.TorBinaryFilePath}'. Attempting to acquire it.");
					return await InstallAsync().ConfigureAwait(false);
				}
				else if (!IoHelpers.CheckExpectedHash(Settings.HashSourcePath, Settings.DistributionFolder))
				{
					Logger.LogInfo("Install the latest Tor version.");
					string backupDir = $"{Settings.TorDir}_backup";

					if (Directory.Exists(backupDir))
					{
						Directory.Delete(backupDir, recursive: true);
					}

					Directory.Move(Settings.TorDir, backupDir);
					return await InstallAsync().ConfigureAwait(false);
				}
				else
				{
					Logger.LogInfo($"Tor instance found at '{Settings.TorBinaryFilePath}'.");
					return true;
				}
			}
			catch (Exception e)
			{
				Logger.LogError("Verification of Tor installation failed.", e);
				return false;
			}
		}

		private async Task ExtractZipFileAsync(string zipFilePath, string destinationPath)
		{
			await IoHelpers.BetterExtractZipToDirectoryAsync(zipFilePath, destinationPath).ConfigureAwait(false);
			Logger.LogInfo($"Extracted '{zipFilePath}' to '{destinationPath}'.");
		}

		private string GetPlatformIdentifier()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return "win64";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return "linux64";
			}
			else
			{
				return "osx64";
			}
		}
	}
}
