using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Microservices;

namespace WalletWasabi.QualityGate.Git.Processes
{
	public class GitProcessBridge : ProcessBridge
	{
		public GitProcessBridge() : base(new BridgeConfiguration(processPath: null, processName: "git"))
		{
		}
	}
}
