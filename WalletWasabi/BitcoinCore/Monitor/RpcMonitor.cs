using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Monitor
{
	public class RpcMonitor : PeriodicRunner
	{
		public RPCClient RpcClient { get; set; }

		public RpcMonitor(TimeSpan period) : base(period, RpcStatus.Unresponsive)
		{
		}

		public override async Task<object> ActionAsync(CancellationToken cancel)
		{
			try
			{
				var bci = await RpcClient.GetBlockchainInfoAsync().ConfigureAwait(false);
				cancel.ThrowIfCancellationRequested();
				var pi = await RpcClient.GetPeersInfoAsync().ConfigureAwait(false);

				return RpcStatus.Responsive(bci.Headers, bci.Blocks, pi.Length);
			}
			catch (Exception ex) when (!(ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException))
			{
				Logger.LogError(ex);
				return RpcStatus.Unresponsive;
			}
		}
	}
}
