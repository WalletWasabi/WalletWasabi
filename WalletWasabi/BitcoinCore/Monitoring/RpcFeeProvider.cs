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
	public class RpcFeeProvider : PeriodicRunner
	{
		public RpcFeeProvider(TimeSpan period, IRPCClient rpcClient) : base(period)
		{
			RpcClient = rpcClient;
		}

		public event EventHandler<AllFeeEstimate>? AllFeeEstimateArrived;

		public IRPCClient RpcClient { get; set; }
		public AllFeeEstimate? LastAllFeeEstimate { get; private set; }
		public bool InError { get; private set; } = false;

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			try
			{
				var allFeeEstimate = await RpcClient.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative, true).ConfigureAwait(false);
				var uptime = await RpcClient.UptimeAsync().ConfigureAwait(false);

				// If Core was running for a day already, then we can be pretty sure that the estimate is accurate.
				// It could also be accurate if Core was only shut down for a few minutes, but that's hard to figure out.
				allFeeEstimate.IsAccurate = uptime > TimeSpan.FromDays(1);

				if (allFeeEstimate?.Estimations?.Any() is true)
				{
					AllFeeEstimateArrived?.Invoke(this, allFeeEstimate);
				}
				LastAllFeeEstimate = allFeeEstimate;
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
