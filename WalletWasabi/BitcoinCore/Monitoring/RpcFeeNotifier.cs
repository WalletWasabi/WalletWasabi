using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.RPC;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore.Monitoring
{
	public class RpcFeeNotifier : PeriodicRunner
	{
		public RpcFeeNotifier(TimeSpan period, IRPCClient rpcClient) : base(period)
		{
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
		}

		public event EventHandler<BestFeeEstimates>? BestFeeEstimatesArrived;

		public IRPCClient RpcClient { get; set; }
		public bool InError { get; private set; } = false;

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			try
			{
				var bestFeeEstimates = await RpcClient.EstimateBestFeesAsync(EstimateSmartFeeMode.Conservative, true).ConfigureAwait(false);
				if (bestFeeEstimates?.Estimations?.Any() is true)
				{
					BestFeeEstimatesArrived?.Invoke(this, bestFeeEstimates);
				}
				InError = false;
			}
			catch
			{
				InError = true;
				throw;
			}
		}
	}
}
