using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.RPC;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Extensions;

namespace WalletWasabi.BitcoinCore.Monitoring;

public class RpcFeeProvider : PeriodicRunner
{
	public RpcFeeProvider(TimeSpan period, IRPCClient rpcClient, RpcMonitor rpcMonitor) : base(period)
	{
		RpcClient = rpcClient;
		RpcMonitor = rpcMonitor;
	}

	public event EventHandler<AllFeeEstimate>? AllFeeEstimateArrived;

	public IRPCClient RpcClient { get; set; }
	public RpcMonitor RpcMonitor { get; }
	public AllFeeEstimate? LastAllFeeEstimate { get; private set; }
	public bool InError { get; private set; } = false;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		try
		{
			var allFeeEstimate = await RpcClient.EstimateAllFeeAsync(cancel).ConfigureAwait(false);

			// If Core was running for a day already && it's synchronized, then we can be pretty sure that the estimate is accurate.
			// It could also be accurate if Core was only shut down for a few minutes, but that's hard to figure out.
			allFeeEstimate.IsAccurate = RpcMonitor.RpcStatus.Synchronized && await RpcClient.UptimeAsync(cancel).ConfigureAwait(false) > TimeSpan.FromDays(1);

			LastAllFeeEstimate = allFeeEstimate;
			if (allFeeEstimate?.Estimations?.Any() is true)
			{
				AllFeeEstimateArrived?.Invoke(this, allFeeEstimate);
			}
			InError = false;
		}
		catch (NoEstimationException)
		{
			Logging.Logger.LogInfo("Couldn't get fee estimation from the Bitcoin node, probably because it was not yet initialized.");
			InError = true;
		}
		catch
		{
			InError = true;
			throw;
		}
	}
}
