using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Helpers;

public static class MacOsStartupHelper
{
	private static readonly string PlistContent =
		$"""
		<?xml version=\"1.0\" encoding=\"UTF-8\"?>
		<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">
		<plist version=\"1.0\">
		<dict>
		    <key>Label</key>
		    <string>com.wasabiwallet.startup</string>
			<key>ProgramArguments</key>
			<array>
				<string>{EnvironmentHelpers.GetExecutablePath()}</string>
				<string>{StartupHelper.SilentArgument}</string>
			</array>
			<key>RunAtLoad</key>
			<true/>
		</dict>
		</plist>";
		""";

	public static async Task AddOrRemoveStartupItemAsync(bool runOnSystemStartup)
	{
		string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		string plistPath = Path.Combine(homeDir, "Library/LaunchAgents/", Constants.SilentPlistName);

		var fileExists = File.Exists(plistPath);
		if (runOnSystemStartup)
		{
			await File.WriteAllTextAsync(plistPath, PlistContent);
		}
		else if (fileExists && !runOnSystemStartup)
		{
			File.Delete(plistPath);
		}
	}
}
