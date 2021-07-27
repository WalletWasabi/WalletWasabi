using System;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Helpers
{
	public static class LinuxStartUpHelper
	{
		public static async Task StartOnLinuxStartupAsync(bool runOnSystemStartup)
		{
			string pathToDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
			string pathToDesktopFile = Path.Combine(pathToDir, "Wasabi.desktop");

			IoHelpers.EnsureContainingDirectoryExists(pathToDesktopFile);

			if (runOnSystemStartup)
			{
				string pathToExec = EnvironmentHelpers.GetExecutablePath();
				string fileContents = string.Join(
					"\n",
					"[Desktop Entry]",
					$"Name={Constants.AppName}",
					"Type=Application",
					$"Exec={pathToExec}",
					"Hidden=false",
					"Terminal=false",
					"X-GNOME-Autostart-enabled=true");

				await File.WriteAllTextAsync(pathToDesktopFile, fileContents).ConfigureAwait(false);
			}
			else
			{
				File.Delete(pathToDesktopFile);
			}
		}
	}
}
