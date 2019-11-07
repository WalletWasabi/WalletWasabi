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
		private RpcStatus _status;

		public RpcStatus Status
		{
			get => _status;
			private set => RaiseAndSetIfChanged(ref _status, value);
		}

		public RPCClient RpcClient { get; set; }

		public RpcMonitor(TimeSpan period) : base(period)
		{
		}

		public override async Task ActionAsync(CancellationToken cancel)
		{
			try
			{
				var bci = await RpcClient.GetBlockchainInfoAsync().ConfigureAwait(false);
				var pi = await RpcClient.GetPeersInfoAsync().ConfigureAwait(false);

				Status = RpcStatus.Responsive(bci.Headers, bci.Blocks, pi.Length);
			}
			catch
			{
				Status = RpcStatus.Unresponsive;
				throw;
			}
		}
	}
}
