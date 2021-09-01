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
	public class BrewManager
	{
		public async Task InstallBrewAsync()
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = "git",
				Arguments = $"clone https://github.com/Homebrew/brew.git",
				RedirectStandardInput = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			};

			using var process = new ProcessAsync(startInfo);

			process.Start();

			await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
		}
	}
}
