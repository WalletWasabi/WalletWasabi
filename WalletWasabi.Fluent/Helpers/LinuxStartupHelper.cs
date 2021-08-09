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

		private static readonly string ExpectedDesktopFileContent = string.Join(
					"\n",
					"[Desktop Entry]",
					$"Name={Constants.AppName}",
					"Type=Application",
					$"Exec={EnvironmentHelpers.GetExecutablePath()}",
					"Hidden=false",
					"Terminal=false",
					"X-GNOME-Autostart-enabled=true");

		public static async Task AddOrRemoveDesktopFileAsync(bool runOnSystemStartup)
		{
			IoHelpers.EnsureContainingDirectoryExists(PathToDesktopFile);

			if (runOnSystemStartup)
			{
				string pathToExec = EnvironmentHelpers.GetExecutablePath();

				IoHelpers.EnsureFileExists(pathToExec);

				string fileContents = string.Join(
					"\n",
					"[Desktop Entry]",
					$"Name={Constants.AppName}",
					"Type=Application",
					$"Exec={pathToExec}",
					"Hidden=false",
					"Terminal=false",
					"X-GNOME-Autostart-enabled=true");

				await File.WriteAllTextAsync(PathToDesktopFile, fileContents).ConfigureAwait(false);
			}
			else
			{
				File.Delete(PathToDesktopFile);
			}
		}

		internal static bool CheckDesktopFile()
		{
			return File.Exists(PathToDesktopFile) && CheckFileContent();
		}

		private static bool CheckFileContent()
		{
			string realFileContent = string.Join("\n", File.ReadAllLines(PathToDesktopFile));

			return string.Equals(ExpectedDesktopFileContent, realFileContent);
		}
	}
}
