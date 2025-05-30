using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.RPC;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinRpc;

public static class RpcMonitor
{
	public static readonly string ServiceName = "BitcoinRpcMonitor";
	public record CheckMessage;

	public static Func<CheckMessage, CancellationToken, Task<Unit>> CreateChecker(IRPCClient rpcClient, EventBus eventBus) =>
		(_, cancellationToken) => CheckRpcStatusAsync(rpcClient, eventBus, cancellationToken);

	private static async Task<Unit> CheckRpcStatusAsync(IRPCClient rpcClient, EventBus eventBus, CancellationToken cancel)
	{
		var rpcStatus = await GetRpcStatusAsync(rpcClient, cancel).ConfigureAwait(false);
		eventBus.Publish(new RpcStatusChanged(rpcStatus));
		return Unit.Instance;
	}

	private static async Task<Result<ConnectedRpcStatus, string>> GetRpcStatusAsync(IRPCClient rpcClient, CancellationToken cancel)
	{
		try
		{
			var bci = await rpcClient.GetBlockchainInfoAsync(cancel).ConfigureAwait(false);
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
				return await rpcClient.GetPeersInfoAsync(cancel).ConfigureAwait(false);
			}
			catch (RPCException e)
			{
				return Result<PeerInfo[], RPCResponse>.Fail(e.RPCResult);
			}
		}
	}
}
