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
		public static async Task InstallAsync(TorSettings settings)
		{
			// Common for all platforms.
			await ExtractZipFileAsync(Path.Combine(settings.DistributionFolder, "data-folder.zip"), settings.TorDir).ConfigureAwait(false);

			// File differs per platform.
			await ExtractZipFileAsync(Path.Combine(settings.DistributionFolder, $"tor-{GetPlatformIdentifier()}.zip"), settings.TorDir).ConfigureAwait(false);

			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				// Set sufficient file permission.
				string shellCommand = $"chmod -R 750 {settings.TorDir}";
				await EnvironmentHelpers.ShellExecAsync(shellCommand, waitForExit: true).ConfigureAwait(false);
				Logger.LogInfo($"Shell command executed: '{shellCommand}'.");
			}
		}

		private static async Task ExtractZipFileAsync(string zipFilePath, string destinationPath)
		{
			await IoHelpers.BetterExtractZipToDirectoryAsync(zipFilePath, destinationPath).ConfigureAwait(false);
			Logger.LogInfo($"Extracted '{zipFilePath}' to '{destinationPath}'.");
		}

		private static string GetPlatformIdentifier()
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
