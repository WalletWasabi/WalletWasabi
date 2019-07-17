using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Interfaces
{
	public interface IProcessBridge
	{
		Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel);
	}
}
