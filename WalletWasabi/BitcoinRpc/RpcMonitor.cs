using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinRpc;

public class RpcMonitor : PeriodicRunner
{
	private readonly EventBus _eventBus;
	private readonly IRPCClient _rpcClient;

	public RpcMonitor(TimeSpan period, IRPCClient rpcClient, EventBus eventBus) : base(period)
	{
		_eventBus = eventBus;
		_rpcClient = rpcClient;
	}


	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var rpcStatus = await GetRpcStatusAsync(cancel).ConfigureAwait(false);
		_eventBus.Publish(new RpcStatusChanged(rpcStatus));
	}


	private async Task<RpcStatus> GetRpcStatusAsync(CancellationToken cancel)
	{
		try
		{
			var bci = await _rpcClient.GetBlockchainInfoAsync(cancel).ConfigureAwait(false);
			var pi = await _rpcClient.GetPeersInfoAsync(cancel).ConfigureAwait(false);
			return new RpcStatus.Responsive(bci.Headers, bci.Blocks, pi.Length, bci.BestBlockHash, bci.Pruned, bci.InitialBlockDownload);
		}
		catch (Exception ex) when (ex is not OperationCanceledException and not TimeoutException)
		{
			Logger.LogTrace(ex);
			return new RpcStatus.Unresponsive();
		}
	}
}
