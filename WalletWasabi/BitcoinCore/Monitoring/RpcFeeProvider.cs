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
	public class RpcFeeProvider : PeriodicRunner, IFeeProvider
	{
		public event EventHandler<AllFeeEstimate> AllFeeEstimateChanged;

		private AllFeeEstimate _allFeeEstimate;

		public AllFeeEstimate AllFeeEstimate
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

		public RPCClient RpcClient { get; set; }

		public RpcFeeProvider(TimeSpan period, RPCClient rpcClient) : base(period)
		{
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
		}

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			try
			{
				var allFeeEstimate = await RpcClient.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative, true, true).ConfigureAwait(false);
				AllFeeEstimate = allFeeEstimate;
			}
			catch
			{
				if (AllFeeEstimate is { })
				{
					AllFeeEstimate = new AllFeeEstimate(AllFeeEstimate, false);
				}
				throw;
			}
		}
	}
}
