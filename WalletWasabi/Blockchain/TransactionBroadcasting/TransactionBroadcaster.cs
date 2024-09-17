using NBitcoin;
using NBitcoin.Protocol;
using System.Linq;
using System.Net;
using System.Net.Http;
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
	public record BroadcastedByRpc : BroadcastOk;

	public record BroadcastedByNetwork(EndPoint[] Nodes) : BroadcastOk;

	public record BroadcastedByBackend() : BroadcastOk;
}

public abstract record BroadcastError
{
	public record SpentError : BroadcastError;
	public record SpentInputError(OutPoint SpentOutpoint) : BroadcastError;
	public record RpcError(string RpcErrorMessage) : BroadcastError;
	public record Unknown(string Message) : BroadcastError;
	public record NotEnoughP2pNodes : BroadcastError;
	public record AggregatedErrors(BroadcastError[] Errors) : BroadcastError;
}

public interface IBroadcaster
{
	Task<BroadcastingResult> BroadcastAsync(SmartTransaction tx);
}

public class RpcBroadcaster(IRPCClient rpcClient) : IBroadcaster
{
	public async Task<BroadcastingResult> BroadcastAsync(SmartTransaction tx)
	{
		Logger.LogInfo($"Trying to broadcast transaction via RPC:{tx.GetHash()}.");
		try
		{
			await rpcClient.SendRawTransactionAsync(tx.Transaction).ConfigureAwait(false);
			return BroadcastingResult.Ok(new BroadcastOk.BroadcastedByRpc());
		}
		catch (RPCException ex)
		{
			return BroadcastingResult.Fail(new BroadcastError.RpcError(ex.RPCCodeMessage));
		}
	}
}

public class BackendBroadcaster(IWasabiHttpClientFactory wasabiHttpClientFactory) : IBroadcaster
{
	public async Task<BroadcastingResult> BroadcastAsync(SmartTransaction tx)
	{
		Logger.LogInfo($"Trying to broadcast transaction via backend API:{tx.GetHash()}.");
		try
		{
			var wasabiClient = new WasabiClient(wasabiHttpClientFactory.NewHttpClientWithCircuitPerRequest());
			await wasabiClient.BroadcastAsync(tx).ConfigureAwait(false);
			return BroadcastingResult.Ok(new BroadcastOk.BroadcastedByBackend());
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
	public async Task<BroadcastingResult> BroadcastAsync(SmartTransaction tx)
	{
		if (nodes.ConnectedNodes.Count < 2)
		{
			return BroadcastingResult.Fail(new BroadcastError.NotEnoughP2pNodes());
		}
		var connectedNodeCount = nodes.ConnectedNodes.Count(x => x.IsConnected);
		var broadcastToNodeTasks = nodes.ConnectedNodes
			.Where(n => n.IsConnected)
			.OrderBy(_ => Guid.NewGuid())
			.Take(2 + connectedNodeCount / 4)
			.Select(n => BroadcastCoreAsync(tx, n))
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
				return result;
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

	private async Task<BroadcastingResult> BroadcastCoreAsync(SmartTransaction tx, Node node)
	{
		Logger.LogInfo($"Trying to broadcast transaction with random node ({node.RemoteSocketAddress}):{tx.GetHash()}.");
		var txId = tx.GetHash();
		if (!mempoolService.TryAddToBroadcastStore(tx)) // So we'll reply to INV with this transaction.
		{
			Logger.LogDebug($"Transaction {txId} was already present in the broadcast store.");
		}
		var invPayload = new InvPayload(tx.Transaction);

		await node.SendMessageAsync(invPayload).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); // ToDo: It's dangerous way to cancel. Implement proper cancellation to NBitcoin!

		if (mempoolService.TryGetFromBroadcastStore(txId, out TransactionBroadcastEntry? entry))
		{
			var broadcastTimeoutTask = Task.Delay(7000);
			var broadcastFinishedTask = await Task.WhenAny([broadcastTimeoutTask, entry.BroadcastCompleted.Task]).ConfigureAwait(false);

			if (broadcastFinishedTask == broadcastTimeoutTask)
			{
				return BroadcastingResult.Fail(new BroadcastError.Unknown($"Timed out to broadcast to {node.RemoteSocketEndpoint} node"));
			}
			node.DisconnectAsync("Thank you!");
			Logger.LogInfo($"Disconnected node: {node.RemoteSocketAddress}. Successfully broadcasted transaction: {txId}.");

			var propagationTimeoutTask = Task.Delay(7000);
			var propagationTask = entry.PropagationConfirmed.Task;
			var propagationFinishedTask = await Task.WhenAny([ propagationTimeoutTask, propagationTask]).ConfigureAwait(false);

			if (propagationFinishedTask == propagationTimeoutTask)
			{
				return BroadcastingResult.Fail(new BroadcastError.Unknown("Timed out to verify propagation."));
			}

			var propagators = await propagationTask.ConfigureAwait(false);
			return BroadcastingResult.Ok(new BroadcastOk.BroadcastedByNetwork(propagators));
		}

		return BroadcastingResult.Fail(new BroadcastError.Unknown($"Expected transaction {txId} was not found in the broadcast store."));
	}
}

public class TransactionBroadcaster(IBroadcaster[] broadcasters, MempoolService mempoolService, WalletManager walletManager)
{
	public async Task SendTransactionAsync(SmartTransaction tx)
	{
		var broadcastedSuccessfully = false;
		await broadcasters
			.ToAsyncEnumerable()
			.SelectAwait(async x => await x.BroadcastAsync(tx).ConfigureAwait(false))
			.TakeUntil(x => x.Match(_ => true, _ => false))
			.ForEachAsync(b => b.MatchDo(
				BroadcastSuccess,
				BroadcastError
				))
			.ConfigureAwait(false);

		if (!broadcastedSuccessfully)
		{
			throw new InvalidOperationException("Error while sending transaction.");
		}

		return;

		void BroadcastSuccess(BroadcastOk ok)
		{
			broadcastedSuccessfully = true;
			switch (ok)
			{
				case BroadcastOk.BroadcastedByBackend:
					Logger.LogInfo($"Transaction is successfully broadcasted {tx.GetHash()} by backend.");
					break;
				case BroadcastOk.BroadcastedByRpc:
					Logger.LogInfo($"Transaction is successfully broadcasted {tx.GetHash()} by local node RPC interface.");
					break;
				case BroadcastOk.BroadcastedByNetwork n:
					foreach (var confirmedPropagators in n.Nodes)
					{
						Logger.LogInfo($"Transaction is successfully broadcasted {tx.GetHash()} and propagated by {confirmedPropagators}.");
					}

					break;
			}
			BelieveTransaction(tx);
		}
	}

	private void BroadcastError(BroadcastError broadcastError)
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
					BroadcastError(error);
				}
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(broadcastError));
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
