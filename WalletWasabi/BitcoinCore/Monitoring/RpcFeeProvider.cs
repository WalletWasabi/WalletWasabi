using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore.Monitoring
{
	public class RpcFeeProvider : PeriodicRunner<AllFeeEstimate>, IFeeProvider
	{
		public RPCClient RpcClient { get; set; }

		public RpcFeeProvider(TimeSpan period, RPCClient rpcClient) : base(period, null)
		{
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
		}

		protected override async Task<AllFeeEstimate> ActionAsync(CancellationToken cancel)
		{
			try
			{
				return await RpcClient.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative, true, true).ConfigureAwait(false);
			}
			catch
			{
				if (Status != null)
				{
					Status = new AllFeeEstimate(Status, false);
				}
				throw;
			}
		}
	}
}
