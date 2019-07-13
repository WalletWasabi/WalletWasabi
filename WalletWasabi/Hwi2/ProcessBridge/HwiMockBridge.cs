using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Hwi2.ProcessBridge
{
	public class HwiMockBridge : IProcessBridge
	{
		public Task<(string response, int exitCode)> SendCommandAsync(string arguments, CancellationToken cancel)
		{
			return Task.FromResult(("mock mock", 0));
		}
	}
}
