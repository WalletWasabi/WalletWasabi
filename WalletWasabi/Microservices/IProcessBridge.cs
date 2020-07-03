using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Microservices
{
	public interface IProcessBridge
	{
		Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel, Action<StreamWriter> standardInputWriter = null);

		Process Start(string arguments, bool openConsole, bool redirectStandardInput);
	}
}
