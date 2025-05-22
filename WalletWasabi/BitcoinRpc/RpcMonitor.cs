using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.RPC;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
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


	private async Task<Result<ConnectedRpcStatus, string>> GetRpcStatusAsync(CancellationToken cancel)
	{
		try
		{
			var bci = await _rpcClient.GetBlockchainInfoAsync(cancel).ConfigureAwait(false);
			var pi = await PeerInfos().ConfigureAwait(false);
			var peerCount = pi.Map(x => x.Length);
			return new ConnectedRpcStatus(bci.Headers, bci.Blocks, peerCount, bci.BestBlockHash, bci.Pruned,
				bci.InitialBlockDownload);
		}
		catch (Exception ex) when (ex is not OperationCanceledException and not TimeoutException)
		{
			Logger.LogTrace(ex);
			return Result<ConnectedRpcStatus, string>.Fail(
				ex switch
				{
					HttpRequestException {StatusCode: HttpStatusCode.Unauthorized} => "Unauthorized (check rpc credentials)",
					HttpRequestException {StatusCode: HttpStatusCode.NotFound} => "RPC sever not found (check rpc uri)",
					HttpRequestException {StatusCode: HttpStatusCode.Forbidden} => "RPC forbidden (check rpc uri)",
					HttpRequestException {StatusCode: null, Message: var msg} => msg,
					_ => "is unresponsive"
				});
		}

		async Task<Result<PeerInfo[], RPCResponse>> PeerInfos()
		{
			try
			{
				return await _rpcClient.GetPeersInfoAsync(cancel).ConfigureAwait(false);
			}
			catch (RPCException e)
			{
				return Result<PeerInfo[], RPCResponse>.Fail(e.RPCResult);
			}
		}
	}
}
