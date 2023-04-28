using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Helpers;

public static class MacOsStartupHelper
{
	private static readonly string AddCmd = $"osascript -e \' tell application \"System Events\" to make new login item at end with properties {{name:\"{Constants.AppName}\", path:\"/Applications/{Constants.AppName}.app/Contents/MacOS/Microservices/Binaries/osx64/{Constants.SilentExecutableName}.app\", hidden:true}} \'";
	private static readonly string AddCmdOG = $"osascript -e \' tell application \"System Events\" to make new login item at end with properties {{name:\"{Constants.AppName}\", path:\"/Applications/{Constants.AppName}.app\", hidden:true}} \'";
	private static readonly string DeleteCmd = $"osascript -e \' tell application \"System Events\" to delete login item \"{Constants.AppName}\" \'";

	public static async Task AddOrRemoveLoginItemAsync(bool runOnSystemStartup)
	{
		if (runOnSystemStartup)
		{
			Logger.LogInfo(AddCmd);
			Logger.LogInfo(AddCmdOG);
			await EnvironmentHelpers.ShellExecAsync(AddCmd).ConfigureAwait(false);
		}
		else
		{
			await EnvironmentHelpers.ShellExecAsync(DeleteCmd).ConfigureAwait(false);
		}
	}
}
