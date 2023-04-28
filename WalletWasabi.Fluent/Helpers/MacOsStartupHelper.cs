using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Helpers;

public static class MacOsStartupHelper
{
	private static readonly string AddCmd = $"osascript -e \' tell application \"System Events\" to make new login item at end with properties {{path:\"/Applications/{Constants.AppName}.app/Contents/MacOS/Microservices/Binaries/osx64/{Constants.SilentExecutableName}.app\", hidden:true}} \'";
	private static readonly string DeleteCmd = $"osascript -e \' tell application \"System Events\" to delete login item \"{Constants.SilentExecutableName}\" \'";

	public static async Task AddOrRemoveLoginItemAsync(bool runOnSystemStartup)
	{
		if (runOnSystemStartup)
		{
			await EnvironmentHelpers.ShellExecAsync(AddCmd).ConfigureAwait(false);
		}
		else
		{
			await EnvironmentHelpers.ShellExecAsync(DeleteCmd).ConfigureAwait(false);
		}
	}
}
