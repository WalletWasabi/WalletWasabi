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

	public static MessageHandler<CheckMessage, Unit> CreateChecker(IRPCClient rpcClient, EventBus eventBus) =>
		(_, _, cancellationToken) => CheckRpcStatusAsync(rpcClient, eventBus, cancellationToken);

	private static async Task<Unit> CheckRpcStatusAsync(IRPCClient rpcClient, EventBus eventBus, CancellationToken cancellationToken)
	{
		var rpcStatus = await GetRpcStatusAsync(rpcClient, cancellationToken).ConfigureAwait(false);
		eventBus.Publish(new RpcStatusChanged(rpcStatus));
		return Unit.Instance;
	}

	private static async Task<Result<ConnectedRpcStatus, string>> GetRpcStatusAsync(IRPCClient rpcClient, CancellationToken cancellationToken)
	{
		try
		{
			var bci = await rpcClient.GetBlockchainInfoAsync(cancellationToken).ConfigureAwait(false);
			var pi = await PeersInfoAsync().ConfigureAwait(false);
			var peerCount = pi.Map(x => x.Length);
			return new ConnectedRpcStatus(bci.Headers, bci.Blocks, peerCount, bci.BestBlockHash, bci.Pruned, bci.InitialBlockDownload);
		}
		catch (Exception ex) when (ex is not OperationCanceledException and not TimeoutException)
		{
			Logger.LogTrace(ex);
			return Result<ConnectedRpcStatus, string>.Fail(
				ex switch
				{
					HttpRequestException {StatusCode: HttpStatusCode.Unauthorized} => "Unauthorized (check RPC credentials)",
					HttpRequestException {StatusCode: HttpStatusCode.NotFound} => "RPC sever not found (check RPC URI)",
					HttpRequestException {StatusCode: HttpStatusCode.Forbidden} => "RPC forbidden (check RPC URI)",
					HttpRequestException {StatusCode: null, Message: var msg} => msg,
					_ => "is unresponsive"
				});
		}

		async Task<Result<PeerInfo[], RPCResponse>> PeersInfoAsync()
		{
			try
			{
				return await rpcClient.GetPeersInfoAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (RPCException e)
			{
				// Connected to a shared readonly RPC.
				return e is {RPCCode:RPCErrorCode.RPC_METHOD_NOT_FOUND}
					? Result<PeerInfo[], RPCResponse>.Ok([new PeerInfo()])
					: Result<PeerInfo[], RPCResponse>.Fail(e.RPCResult);
			}
		}
	}
}
