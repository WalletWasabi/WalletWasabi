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
		public static async Task InstallAsync(string torDir)
		{
			string torDaemonsDir = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "TorDaemons");

			string dataZip = Path.Combine(torDaemonsDir, "data-folder.zip");
			await IoHelpers.BetterExtractZipToDirectoryAsync(dataZip, torDir).ConfigureAwait(false);
			Logger.LogInfo($"Extracted {dataZip} to {torDir}.");

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				string torWinZip = Path.Combine(torDaemonsDir, "tor-win64.zip");
				await IoHelpers.BetterExtractZipToDirectoryAsync(torWinZip, torDir).ConfigureAwait(false);
				Logger.LogInfo($"Extracted {torWinZip} to {torDir}.");
			}
			else // Linux or OSX
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					string torLinuxZip = Path.Combine(torDaemonsDir, "tor-linux64.zip");
					await IoHelpers.BetterExtractZipToDirectoryAsync(torLinuxZip, torDir).ConfigureAwait(false);
					Logger.LogInfo($"Extracted {torLinuxZip} to {torDir}.");
				}
				else // OSX
				{
					string torOsxZip = Path.Combine(torDaemonsDir, "tor-osx64.zip");
					await IoHelpers.BetterExtractZipToDirectoryAsync(torOsxZip, torDir).ConfigureAwait(false);
					Logger.LogInfo($"Extracted {torOsxZip} to {torDir}.");
				}

				// Make sure there's sufficient permission.
				string chmodTorDirCmd = $"chmod -R 750 {torDir}";
				await EnvironmentHelpers.ShellExecAsync(chmodTorDirCmd, waitForExit: true).ConfigureAwait(false);
				Logger.LogInfo($"Shell command executed: {chmodTorDirCmd}.");
			}
		}
	}
}
