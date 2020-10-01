using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Core
{
	public class InitConfigStartupTask : IStartupTask
	{
		public InitConfigStartupTask(Global global)
		{
			Global = global;
		}

		public Global Global { get; }

		public async Task ExecuteAsync(CancellationToken cancellationToken)
		{
			Logger.InitializeDefaults(Path.Combine(Global.DataDir, "CoreLogs.txt"));
			Logger.LogSoftwareStarted("Wasabi Core");
		}
	}
}
