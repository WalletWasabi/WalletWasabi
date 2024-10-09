using NBitcoin;
using NBitcoin.Protocol;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.RPC;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Blockchain.TransactionBroadcasting;

using BroadcastingResult = Result<BroadcastOk, BroadcastError>;


public abstract record BroadcastOk
{
	public record BroadcastByRpc : BroadcastOk;

	public record BroadcastByNetwork(EndPoint[] Nodes) : BroadcastOk;

	public record BroadcastByBackend : BroadcastOk;
}

public abstract record BroadcastError
{
	public record SpentError : BroadcastError;
	public record SpentInputError(OutPoint SpentOutpoint) : BroadcastError;
	public record RpcError(string RpcErrorMessage) : BroadcastError;
	public record Unknown(string Message) : BroadcastError;
	public record NotEnoughP2pNodes : BroadcastError;
	public record AggregatedErrors(BroadcastError[] Errors) : BroadcastError;
	public record Timeout(string Message) : BroadcastError;

}

public interface IBroadcaster
{
	Task<BroadcastingResult> BroadcastAsync(SmartTransaction tx, CancellationToken cancellationToken);
}

public class RpcBroadcaster(IRPCClient rpcClient) : IBroadcaster
{
	public async Task<BroadcastingResult> BroadcastAsync(SmartTransaction tx, CancellationToken cancellationToken)
	{
		Logger.LogInfo($"Trying to broadcast transaction via RPC:{tx.GetHash()}.");
		try
		{
			await rpcClient.SendRawTransactionAsync(tx.Transaction, cancellationToken).ConfigureAwait(false);
			return BroadcastingResult.Ok(new BroadcastOk.BroadcastByRpc());
		}
		catch (RPCException ex)
		{
			return BroadcastingResult.Fail(new BroadcastError.RpcError(ex.RPCCodeMessage));
		}
	}
}

public class BackendBroadcaster(IHttpClientFactory httpClientFactory) : IBroadcaster
{
	public async Task<BroadcastingResult> BroadcastAsync(SmartTransaction tx, CancellationToken cancellationToken)
	{
		Logger.LogInfo($"Trying to broadcast transaction via backend API:{tx.GetHash()}.");
		try
		{
			var wasabiClient = new WasabiClient(httpClientFactory.CreateClient($"satoshi-broadcast-{tx.GetHash()}"));
			await wasabiClient.BroadcastAsync(tx, cancellationToken).ConfigureAwait(false);
			return BroadcastingResult.Ok(new BroadcastOk.BroadcastByBackend());
		}
		catch (HttpRequestException ex)
		{
			if (RpcErrorTools.IsSpentError(ex.Message))
			{
				// If there is only one coin then that's the one that is already spent (what about full-RBF?).
				if (tx.Transaction.Inputs.Count == 1)
				{
					OutPoint input = tx.Transaction.Inputs[0].PrevOut;
					return BroadcastingResult.Fail(new BroadcastError.SpentInputError(input));
				}

				return BroadcastingResult.Fail(new BroadcastError.SpentError());
			}
			return BroadcastingResult.Fail(new BroadcastError.Unknown(ex.Message));
		}
	}
}

public class NetworkBroadcaster(MempoolService mempoolService, NodesGroup nodes) : IBroadcaster
{
	public const int MinBroadcastNodes = 2;
	public async Task<BroadcastingResult> BroadcastAsync(SmartTransaction tx, CancellationToken cancellationToken)
	{
		var connectedNodes = nodes.ConnectedNodes.Where(x => x.IsConnected).ToArray();
		if (connectedNodes.Length < MinBroadcastNodes)
		{
			return BroadcastingResult.Fail(new BroadcastError.NotEnoughP2pNodes());
		}

		var broadcastToNode = connectedNodes
			.Where(n => n.IsConnected)
			.OrderBy(_ => Guid.NewGuid())
			.Take(Math.Max(MinBroadcastNodes, 1 + connectedNodes.Length / 5))
			.ToArray();

		var broadcastToNodeTasks = broadcastToNode
			.Select(n => BroadcastCoreAsync(tx, n, cancellationToken))
			.ToList();

		var tasksToWaitFor = broadcastToNodeTasks.ToList();
		Task<BroadcastingResult> completedTask;
		do
		{
			completedTask = await Task.WhenAny(tasksToWaitFor).ConfigureAwait(false);
			tasksToWaitFor.Remove(completedTask);
			var result = await completedTask.ConfigureAwait(false);
			if (result.IsOk)
			{

				var confirmation = await ConfirmPropagationAsync(tx, cancellationToken).ConfigureAwait(false);
				foreach (var node in broadcastToNode)
				{
					node.DisconnectAsync();
				}

				return confirmation;
			}
		} while (completedTask.IsFaulted && tasksToWaitFor.Count > 0);

		var results = await Task.WhenAll(broadcastToNodeTasks).ConfigureAwait(false);
		var errors = results
			.Select(r => r.Match(_ => (IsError: false, Error: null)!, e => (IsError: true, Error: e)))
			.Where(x => x.IsError)
			.Select(x => x.Error)
			.ToArray();
		return BroadcastingResult.Fail(new BroadcastError.AggregatedErrors(errors));
	}

	private async Task<BroadcastingResult> BroadcastCoreAsync(SmartTransaction tx, Node node, CancellationToken cancellationToken)
	{
		Logger.LogInfo($"Trying to broadcast transaction with random node ({node.RemoteSocketAddress}):{tx.GetHash()}.");
		var entry = mempoolService.GetOrAdd(tx);
		var invPayload = new InvPayload(tx.Transaction);

		await node.SendMessageAsync(invPayload).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);

		var broadcastTimeoutTask = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
		var broadcastTask = entry.BroadcastCompleted.Task;
		var broadcastFinishedTask = await Task.WhenAny([broadcastTimeoutTask, broadcastTask]).ConfigureAwait(false);

		if (broadcastFinishedTask == broadcastTimeoutTask)
		{
			return BroadcastingResult.Fail(new BroadcastError.Unknown($"Timed out to broadcast to {node.RemoteSocketEndpoint} node"));
		}

		var broadcasters = await broadcastTask.ConfigureAwait(false);
		return BroadcastingResult.Ok(new BroadcastOk.BroadcastByNetwork(broadcasters));
	}

	private	async Task<BroadcastingResult> ConfirmPropagationAsync(SmartTransaction tx, CancellationToken cancellationToken)
	{
		var entry = mempoolService.GetOrAdd(tx);
		var propagationTimeoutTask = Task.Delay(TimeSpan.FromSeconds(12), cancellationToken);
		var propagationTask = entry.PropagationConfirmed.Task;
		var propagationFinishedTask = await Task.WhenAny([propagationTimeoutTask, propagationTask]).ConfigureAwait(false);

		if (propagationFinishedTask == propagationTimeoutTask)
		{
			BroadcastingResult.Fail(new BroadcastError.Timeout("Timed out to verify propagation"));
		}

		var propagators = await propagationTask.ConfigureAwait(false);
		return BroadcastingResult.Ok(new BroadcastOk.BroadcastByNetwork(propagators));
	}
}

public class TransactionBroadcaster(IBroadcaster[] broadcasters, MempoolService mempoolService, WalletManager walletManager)
{
	public async Task SendTransactionAsync(SmartTransaction tx, CancellationToken cancellationToken = default)
	{
		var results = await broadcasters
			.ToAsyncEnumerable()
			.SelectAwait(async x => await x.BroadcastAsync(tx, cancellationToken).ConfigureAwait(false))
			.TakeUntil(x => x.IsOk)
			.ToArrayAsync(cancellationToken)
			.ConfigureAwait(false);

		foreach (var error in results.TakeWhile(x => !x.IsOk).Select(x => x.Error))
		{
			ReportError(error);
		}

		if (results.LastOrDefault(x => x.IsOk) is not { } ok)
		{
			throw new InvalidOperationException("Error while sending transaction.");
		}

		ReportSuccessfulBroadcast(ok.Value, tx.GetHash());

		BelieveTransaction(tx);
	}

	private void ReportError(BroadcastError broadcastError)
	{
		switch (broadcastError)
		{
			case BroadcastError.RpcError rpcError:
				Logger.LogInfo($"Failed to broadcast transaction via RPC. Reason: {rpcError.RpcErrorMessage}.");
				break;
			case BroadcastError.SpentError _:
				Logger.LogError("Failed to broadcast transaction. There are spent inputs.");
				break;
			case BroadcastError.SpentInputError spentInputError:
				Logger.LogError($"Failed to broadcast transaction. Input {spentInputError.SpentOutpoint} is already spent.");
				foreach (var coin in walletManager.CoinsByOutPoint(spentInputError.SpentOutpoint))
				{
					coin.SpentAccordingToBackend = true;
				}
				break;
			case BroadcastError.NotEnoughP2pNodes _:
				Logger.LogInfo("Failed to broadcast transaction via peer-to-peer network: We are not connected to enough nodes.");
				break;
			case BroadcastError.Unknown unknown:
				Logger.LogInfo($"Failed to broadcast transaction: {unknown.Message}.");
				break;
			case BroadcastError.AggregatedErrors aggregatedErrors:
				foreach (var error in aggregatedErrors.Errors)
				{
					ReportError(error);
				}
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(broadcastError));
		}
	}

	private	void ReportSuccessfulBroadcast(BroadcastOk ok, uint256 txId)
	{
		switch (ok)
		{
			case BroadcastOk.BroadcastByBackend:
				Logger.LogInfo($"Transaction is successfully broadcast {txId} by backend.");
				break;
			case BroadcastOk.BroadcastByRpc:
				Logger.LogInfo($"Transaction is successfully broadcast {txId} by local node RPC interface.");
				break;
			case BroadcastOk.BroadcastByNetwork n:
				foreach (var propagator in n.Nodes)
				{
					Logger.LogInfo($"Transaction is successfully progagated {txId} confirmed by {propagator}.");
				}
				break;
		}
	}

	private void BelieveTransaction(SmartTransaction transaction)
	{
		if (transaction.Height == Height.Unknown)
		{
			transaction.SetUnconfirmed();
		}

		mempoolService.TryAddToBroadcastStore(transaction);

		walletManager.Process(transaction);
	}
}

public static class MempoolServiceExtensions
{
	public static TransactionBroadcastEntry GetOrAdd(this MempoolService mempoolService, SmartTransaction tx)
	{
		var txId = tx.GetHash();
		if (!mempoolService.TryAddToBroadcastStore(tx)) // So we'll reply to INV with this transaction.
		{
			Logger.LogDebug($"Transaction {txId} was already present in the broadcast store.");
		}
		if (!mempoolService.TryGetFromBroadcastStore(txId, out TransactionBroadcastEntry? entry))
		{
			throw new InvalidOperationException($"Transaction {txId} was not found in the broadcast store.");
		}

		return entry;
	}
}
