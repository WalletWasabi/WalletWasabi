using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Helpers;

public static class MacOsStartupHelper
{
	private static readonly string AddCmd = $"osascript -e \' tell application \"System Events\" to make new login item at end with properties {{name:\"{Constants.AppName}\", path:\"/Applications/{Constants.AppName}.app\", hidden:true}} \'";
	private static readonly string DeleteCmd = $"osascript -e \' tell application \"System Events\" to delete login item \"{Constants.AppName}\" \'";

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
