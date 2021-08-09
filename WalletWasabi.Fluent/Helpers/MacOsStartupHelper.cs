using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Microservices;

namespace WalletWasabi.Fluent.Helpers
{
	public static class MacOsStartupHelper
	{
		private static readonly string AddCmd = $"osascript -e \' tell application \"System Events\" to make new login item at end with properties {{name:\"{Constants.AppName}\", path:\"/Applications/{Constants.AppName}.app\", hidden:true}} \'";
		private static readonly string DeleteCmd = $"osascript -e \' tell application \"System Events\" to delete login item \"{Constants.AppName}\" \'";
		private static readonly string ListCmd = $"osascript -e \' tell application \"System Events\" to get every login item\'";

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

		internal async static Task<bool> CheckLoginItemExistsAsync()
		{
			var escapedArgs = ListCmd.Replace("\"", "\\\"");

			var startInfo = new ProcessStartInfo
			{
				FileName = "/usr/bin/env",
				Arguments = $"sh -c \"{escapedArgs}\"",
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			};

			using var process = new ProcessAsync(startInfo);

			process.Start();

			string output = process.StandardOutput.ReadToEnd();

			await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

			return output.Contains(Constants.AppName);
		}
	}
}
