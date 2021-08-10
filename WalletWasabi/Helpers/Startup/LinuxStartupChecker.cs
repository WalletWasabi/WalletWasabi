using System;
using System.IO;
using System.Threading.Tasks;

namespace WalletWasabi.Helpers
{
	public static class LinuxStartupChecker
	{
		private static readonly string DesktopFileContent = string.Join(
					"\n",
					"[Desktop Entry]",
					$"Name={Constants.AppName}",
					"Type=Application",
					$"Exec={EnvironmentHelpers.GetExecutablePath()}",
					"Hidden=false",
					"Terminal=false",
					"X-GNOME-Autostart-enabled=true");

		private static string PathToDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
		private static string PathToDesktopFile = Path.Combine(PathToDir, "Wasabi.desktop");

		public static async Task<bool> CheckDesktopFileAsync()
		{
			bool result = false;
			if (File.Exists(PathToDesktopFile))
			{
				string realFileContent = string.Join("\n", await File.ReadAllLinesAsync(PathToDesktopFile).ConfigureAwait(false));

				result = string.Equals(DesktopFileContent, realFileContent);
			}
			return result;
		}
	}
}
