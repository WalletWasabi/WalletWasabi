using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.Hwi.ProcessBridge
{
	public class HwiProcessBridge : Microservices.ProcessBridge
	{
		public HwiProcessBridge() : base(EnvironmentHelpers.GetBinaryPath("Hwi", "hwi"))
		{
		}

		public new async Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel)
		{
			var redirectStandardOutput = !openConsole;

			var (responseString, exitCode) = await base.SendCommandAsync(arguments, openConsole, cancel).ConfigureAwait(false);

			string modifiedResponseString;
			if (redirectStandardOutput)
			{
				modifiedResponseString = responseString;
			}
			else
			{
				modifiedResponseString = exitCode == 0
					? "{\"success\":\"true\"}"
					: $"{{\"success\":\"false\",\"error\":\"Process terminated with exit code: {exitCode}.\"}}";
			}

			return (modifiedResponseString, exitCode);
		}
	}
}
