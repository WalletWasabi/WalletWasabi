using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Monitoring
{
	public class RpcMonitor : PeriodicRunner
	{
		public RPCClient RpcClient { get; set; }

		public RpcMonitor(TimeSpan period) : base(period, RpcStatus.Unresponsive)
		{
		}

		public override async Task<object> ActionAsync(CancellationToken cancel)
		{
			return await RpcClient.GetRpcStatusAsync(cancel).ConfigureAwait(false);
		}
	}
}
