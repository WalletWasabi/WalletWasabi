using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Helpers;

public static class LinuxStartupHelper
{
	public static async Task AddOrRemoveDesktopFileAsync(bool runOnSystemStartup)
	{
		string pathToDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
		string pathToDesktopFile = Path.Combine(pathToDir, "Wasabi.desktop");

		IoHelpers.EnsureContainingDirectoryExists(pathToDesktopFile);

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

			await File.WriteAllTextAsync(pathToDesktopFile, fileContents).ConfigureAwait(false);
		}
		else
		{
			File.Delete(pathToDesktopFile);
		}
	}
}
