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
		private RpcStatus _rpcStatus;

		public RPCClient RpcClient { get; set; }

		public RpcStatus RpcStatus
		{
			get => _rpcStatus;
			private set
			{
				if (value != _rpcStatus)
				{
					_rpcStatus = value;
					RpcStatusChanged?.Invoke(this, value);
				}
			}
		}

		public event EventHandler<RpcStatus> RpcStatusChanged;

		public RpcMonitor(TimeSpan period, RPCClient rpcClient) : base(period)
		{
			RpcStatus = RpcStatus.Unresponsive;
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
		}

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			RpcStatus = await RpcClient.GetRpcStatusAsync(cancel).ConfigureAwait(false);
		}
	}
}
