using System;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Helpers
{
	public static class LinuxStartupHelper
	{
		private static string PathToDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
		private static string PathToDesktopFile = Path.Combine(PathToDir, "Wasabi.desktop");

		public static void AddOrRemoveDesktopFile(bool runOnSystemStartup)
		{
			IoHelpers.EnsureContainingDirectoryExists(PathToDesktopFile);

			if (runOnSystemStartup)
			{
				string pathToExec = EnvironmentHelpers.GetExecutablePath();

				IoHelpers.EnsureFileExists(pathToExec);

				string fileContent = string.Join(
					"\n",
					"[Desktop Entry]",
					$"Name={Constants.AppName}",
					"Type=Application",
					$"Exec={pathToExec}",
					"Hidden=false",
					"Terminal=false",
					"X-GNOME-Autostart-enabled=true");

				File.WriteAllText(PathToDesktopFile, fileContent);
			}
			else
			{
				File.Delete(PathToDesktopFile);
			}
		}
	}
}
