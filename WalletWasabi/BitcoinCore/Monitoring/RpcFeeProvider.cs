using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.RPC;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore.Monitoring
{
	public class RpcFeeProvider : PeriodicRunner, IFeeProvider
	{
		private BestFeeEstimates _allFeeEstimate;

		public RpcFeeProvider(TimeSpan period, IRPCClient rpcClient) : base(period)
		{
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
		}

		public event EventHandler<BestFeeEstimates>? AllFeeEstimateChanged;

		public BestFeeEstimates AllFeeEstimate
		{
			get => _allFeeEstimate;
			private set
			{
				if (value != _allFeeEstimate)
				{
					_allFeeEstimate = value;
					AllFeeEstimateChanged?.Invoke(this, value);
				}
			}
		}

		public IRPCClient RpcClient { get; set; }

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			try
			{
				var allFeeEstimate = await RpcClient.EstimateBestFeesAsync(EstimateSmartFeeMode.Conservative, true).ConfigureAwait(false);
				AllFeeEstimate = allFeeEstimate;
			}
			catch
			{
				if (AllFeeEstimate is { })
				{
					AllFeeEstimate = new BestFeeEstimates(AllFeeEstimate, false);
				}
				throw;
			}
		}
	}
}
