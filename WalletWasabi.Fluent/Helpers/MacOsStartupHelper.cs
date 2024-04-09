using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Helpers;

public static class MacOsStartupHelper
{
	private static readonly string ListCmd = $"""osascript -e 'tell application "System Events" to get the name of every login item'""";
	private static readonly string DeleteCmd = $"""osascript -e 'tell application "System Events" to delete login item "{Constants.AppName}"'""";

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
		</plist>
		""";

	public static async Task AddOrRemoveStartupItemAsync(bool runOnSystemStartup)
	{
		await DeleteLoginItemIfExistsAsync().ConfigureAwait(false);

		string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		string plistPath = Path.Combine(homeDir, "Library/LaunchAgents/", Constants.SilentPlistName);

		var fileExists = File.Exists(plistPath);
		if (runOnSystemStartup)
		{
			await File.WriteAllTextAsync(plistPath, PlistContent).ConfigureAwait(false);
		}
		else if (fileExists && !runOnSystemStartup)
		{
			File.Delete(plistPath);
		}
	}

	private static async Task DeleteLoginItemIfExistsAsync()
	{
		// From 2.0.6, we use LaunchAgents instead of Login Items to run Wasabi hidden during startup. We need to delete older existing Login Items.
		// https://github.com/zkSNACKs/WalletWasabi/pull/12772#pullrequestreview-1984574457
		string result = await EnvironmentHelpers.ShellExecAndGetResultAsync(ListCmd).ConfigureAwait(false);
		bool loginItemExists = result.Contains(Constants.AppName);
		if (loginItemExists)
		{
			await EnvironmentHelpers.ShellExecAsync(DeleteCmd).ConfigureAwait(false);
		}
	}
}
