using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Microservices;

namespace WalletWasabi.Helpers
{
	public static class MacOsStartupChecker
	{
		private static readonly string ListCmd = $"osascript -e \' tell application \"System Events\" to get every login item\'";

		internal static async Task<bool> CheckLoginItemExistsAsync()
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

			string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);

			await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

			return output.Contains(Constants.AppName);
		}
	}
}
