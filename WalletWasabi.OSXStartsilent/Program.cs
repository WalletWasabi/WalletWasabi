using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.OSXStartsilent;

public class Program
{
	private static void Main()
	{
		try
		{
			string arg = "osascript -e 'do shell script \"open -a /Applications/Wasabi\\\\ Wallet.app --args startsilent\"'";
			EnvironmentHelpers.ShellExecAsync(arg).Wait();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}
}
