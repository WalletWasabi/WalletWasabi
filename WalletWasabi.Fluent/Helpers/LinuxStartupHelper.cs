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

		private static readonly string DesktopFileContent = string.Join(
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

				await File.WriteAllTextAsync(PathToDesktopFile, DesktopFileContent).ConfigureAwait(false);
			}
			else
			{
				File.Delete(PathToDesktopFile);
			}
		}

		internal static bool CheckDesktopFile()
		{
			bool result = false;
			if (File.Exists(PathToDesktopFile))
			{
				result = CheckFileContent();
			}
			return result;
		}

		private static bool CheckFileContent()
		{
			string realFileContent = string.Join("\n", File.ReadAllLines(PathToDesktopFile));

			return string.Equals(DesktopFileContent, realFileContent);
		}
	}
}
