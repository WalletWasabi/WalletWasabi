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

		public RpcStatus RpcStatus { get; private set; }

		public event EventHandler<RpcStatus> RpcStatusChanged;

		public RpcMonitor(TimeSpan period, RPCClient rpcClient) : base(period)
		{
			RpcStatus = RpcStatus.Unresponsive;
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
		}

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			var rpcStatus = await RpcClient.GetRpcStatusAsync(cancel).ConfigureAwait(false);
			if (rpcStatus != RpcStatus)
			{
				RpcStatus = rpcStatus;
				RpcStatusChanged?.Invoke(this, rpcStatus);
			}
		}
	}
}
