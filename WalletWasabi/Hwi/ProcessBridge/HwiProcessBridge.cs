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
		private string _content;

		public HwiProcessBridge() : base(MicroserviceHelpers.GetBinaryPath("hwi"))
		{
			_content = string.Empty;
		}

		public new async Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel)
		{
			try
			{
				if (arguments.Contains("--stdin"))
				{
					var lastArgumentStartIndex = arguments.LastIndexOf(' ');
					_content = arguments[(lastArgumentStartIndex+1)..];
					arguments = arguments[..lastArgumentStartIndex];
				}
				var (responseString, exitCode) = await base.SendCommandAsync(arguments, openConsole, cancel).ConfigureAwait(false);

				string modifiedResponseString;

				if (!openConsole)
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
			finally
			{
				_content = string.Empty;
			}
		}

		protected override void Send(StreamWriter input)
		{
			input.WriteLine(_content);
			input.WriteLine();
			input.Flush();
		}
	}
}
