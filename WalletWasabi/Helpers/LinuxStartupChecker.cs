using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

		public static bool CheckDesktopFile()
		{
			bool result = false;
			if (File.Exists(PathToDesktopFile))
			{
				string realFileContent = string.Join("\n", File.ReadAllLines(PathToDesktopFile));

				result = string.Equals(DesktopFileContent, realFileContent);
			}
			return result;
		}
	}
}
