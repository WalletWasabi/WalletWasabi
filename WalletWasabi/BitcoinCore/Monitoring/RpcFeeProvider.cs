using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;

namespace WalletWasabi.BlockchainAnalysis.FeesEstimation
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
		public bool TolerateFailure { get; }

		public RpcFeeProvider(TimeSpan period, RPCClient rpcClient, bool tolerateFailure) : base(period, null)
		{
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
			TolerateFailure = tolerateFailure;
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
				if (TolerateFailure)
				{
					// Will be cought by the periodic runner.
					// The point is that the result isn't changing to null, so it can still serve the latest fee it knew about.
					throw;
				}
				else
				{
					// Sometimes we want to report failure so our secondary mechanisms can take over its place.
					AllFeeEstimate = null;
					return null;
				}
			}
		}
	}
}
