using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;

namespace WalletWasabi.Services
{
	public class MempoolSaver : PeriodicRunner
	{
		public MempoolSaver(TimeSpan period, IRPCClient rpc) : base(period)
		{
			RpcClient = rpc;
		}

		public IRPCClient RpcClient { get; }

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			await RpcClient.SaveMempoolAsync().ConfigureAwait(false);
		}
	}
}
