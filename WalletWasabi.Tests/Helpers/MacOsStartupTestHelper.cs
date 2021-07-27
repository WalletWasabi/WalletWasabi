using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Microservices;

namespace WalletWasabi.Tests.Helpers
{
	public class MacOsStartupTestHelper
	{
		private string _listCmd = $"osascript -e \' tell application \"System Events\" to get the name of every login item\'";

		public async Task<string> GetLoginItemsAsync()
		{
			var escapedArgs = _listCmd.Replace("\"", "\\\"");

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

			string output = process.StandardOutput.ReadToEnd();  // Gives back "login item Wasabi Wallet" or "login item"

			await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

			return output;
		}
	}
}
