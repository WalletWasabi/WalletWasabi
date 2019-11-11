using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Bases;
using WalletWasabi.BlockchainAnalysis.FeesEstimation;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore.Monitoring
{
	public class RpcFeeProvider : PeriodicRunner, IFeeProvider
	{
		private AllFeeEstimate _allFeeEstimate;

		public AllFeeEstimate AllFeeEstimate
		{
			get => _allFeeEstimate;
			private set => RaiseAndSetIfChanged(ref _allFeeEstimate, value);
		}

		public RPCClient RpcClient { get; set; }

		public RpcFeeProvider(TimeSpan period, RPCClient rpcClient) : base(period, null)
		{
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
		}

		public override async Task<object> ActionAsync(CancellationToken cancel)
		{
			try
			{
				var afs = await RpcClient.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative, true, true).ConfigureAwait(false);

				AllFeeEstimate = afs;
				return afs;
			}
			catch
			{
				AllFeeEstimate = null;
				throw;
			}
		}
	}
}
