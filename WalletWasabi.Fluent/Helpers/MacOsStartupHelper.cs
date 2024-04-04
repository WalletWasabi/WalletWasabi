using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Helpers;

public static class MacOsStartupHelper
{
	private static readonly string PlistContent = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\r\n<plist version=\"1.0\">\r\n<dict>\r\n    <key>Label</key>\r\n    <string>com.wasabiwallet.startup</string>\r\n    <key>ProgramArguments</key>\r\n    <array>\r\n        <string>/Applications/Wasabi Wallet.app/Contents/MacOS/wassabee</string>\r\n        <string>startsilent</string>\r\n    </array>\r\n    <key>RunAtLoad</key>\r\n    <true/>\r\n</dict>\r\n</plist>";

	public static async Task AddOrRemoveLoginItemAsync(bool runOnSystemStartup)
	{
		string dataDir = "~/Library/LaunchAgents/";
		string plistPath = Path.Combine(dataDir, Constants.SilentPlistName);
		var fileExists = File.Exists(plistPath);
		if (!fileExists && runOnSystemStartup)
		{
			await File.WriteAllTextAsync(plistPath, PlistContent);
		}
		else if (fileExists && !runOnSystemStartup)
		{
			File.Delete(plistPath);
		}
	}
}
