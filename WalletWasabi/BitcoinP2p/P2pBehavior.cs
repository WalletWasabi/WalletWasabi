using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinP2p;

public class P2pBehavior : NodeBehavior
{
	private const int MaxInvSize = 50000;

	public P2pBehavior(MempoolService mempoolService)
	{
		MempoolService = Guard.NotNull(nameof(mempoolService), mempoolService);
	}

	public MempoolService MempoolService { get; }

	protected override void AttachCore()
	{
		AttachedNode.MessageReceived += AttachedNode_MessageReceivedAsync;
	}

	protected override void DetachCore()
	{
		AttachedNode.MessageReceived -= AttachedNode_MessageReceivedAsync;
	}

	private async void AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
	{
		try
		{
			if (message.Message.Payload is GetDataPayload getDataPayload)
			{
				await ProcessGetDataAsync(node, getDataPayload).ConfigureAwait(false);
			}
			else if (message.Message.Payload is TxPayload txPayload)
			{
				ProcessTx(txPayload);
			}
			else if (message.Message.Payload is InvPayload invPayload)
			{
				await ProcessInventoryAsync(node, invPayload).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException ex)
		{
			Logger.LogDebug(ex);
		}
		catch (Exception ex)
		{
			Logger.LogInfo($"Ignoring {ex.GetType()}: {ex.Message}");
			Logger.LogDebug(ex);
		}
	}

	private async Task ProcessInventoryAsync(Node node, InvPayload invPayload)
	{
		var getDataPayload = new GetDataPayload();
		foreach (var inv in invPayload.Inventory)
		{
			if (ProcessInventoryVector(inv, node.RemoteSocketEndpoint))
			{
				getDataPayload.Inventory.Add(new InventoryVector(node.AddSupportedOptions(inv.Type), inv.Hash));
			}
		}
		if (getDataPayload.Inventory.Count != 0 && node.IsConnected)
		{
			await node.SendMessageAsync(getDataPayload).ConfigureAwait(false);
		}
	}

	private bool ProcessInventoryVector(InventoryVector inv, EndPoint remoteSocketEndpoint)
	{
		if (inv.Type.HasFlag(InventoryType.MSG_TX))
		{
			if (MempoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry? entry)) // If we have the transaction then adjust confirmation.
			{
				entry.ConfirmPropagationOnce(remoteSocketEndpoint);
			}

			// If we already processed it, then don't ask for it.
			if (MempoolService.IsProcessed(inv.Hash))
			{
				return false;
			}

			return true;
		}

		return false;
	}

	private async Task ProcessGetDataAsync(Node node, GetDataPayload payload)
	{
		if (payload.Inventory.Count > MaxInvSize)
		{
			Logger.LogDebug($"Received inventory too big. {nameof(MaxInvSize)}: {MaxInvSize}, Node: {node.RemoteSocketEndpoint}");
			return;
		}

		foreach (var inv in payload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX) || inv.Type.HasFlag(InventoryType.MSG_WTX)))
		{
			if (MempoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry? entry)) // If we have the transaction to be broadcasted then broadcast it now.
			{
				if (!node.IsConnected)
				{
					Logger.LogDebug($"Could not serve transaction. Node ({node.RemoteSocketEndpoint}) is not connected anymore: {entry.TransactionId}.");
				}
				else
				{
					var txPayload = new TxPayload(entry.Transaction.Transaction);
					await node.SendMessageAsync(txPayload).ConfigureAwait(false);
					entry.BroadcastedTo(node.RemoteSocketEndpoint);
					Logger.LogDebug($"Successfully served transaction to node ({node.RemoteSocketEndpoint}): {entry.TransactionId}.");
				}
			}
		}
	}

	private void ProcessTx(TxPayload payload)
	{
		Transaction transaction = payload.Object;
		transaction.PrecomputeHash(false, true);
		MempoolService.Process(transaction);
	}

	public override object Clone() => new P2pBehavior(MempoolService);
}
