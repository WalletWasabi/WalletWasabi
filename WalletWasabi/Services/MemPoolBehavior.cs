using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Services
{
	public class MemPoolBehavior : NodeBehavior
	{
		private const int MAX_INV_SIZE = 50000;

		public MemPoolService MemPoolService { get; }

		public MemPoolBehavior(MemPoolService memPoolService)
		{
			MemPoolService = Guard.NotNull(nameof(memPoolService), memPoolService);
		}

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
					await ProcessGetDataAsync(node, getDataPayload);
					return;
				}

				if (message.Message.Payload is TxPayload txPayload)
				{
					ProcessTx(txPayload);
					return;
				}

				if (message.Message.Payload is InvPayload invPayload)
				{
					await ProcessInvAsync(node, invPayload);
					return;
				}
			}
			catch (OperationCanceledException ex)
			{
				Logger.LogDebug<MemPoolBehavior>(ex);
			}
			catch (Exception ex)
			{
				Logger.LogInfo<MemPoolBehavior>($"Ignoring {ex.GetType()}: {ex.Message}");
				Logger.LogDebug<MemPoolBehavior>(ex);
			}
		}

		private async Task ProcessGetDataAsync(Node node, GetDataPayload payload)
		{
			if (payload.Inventory.Count > MAX_INV_SIZE)
			{
				Logger.LogDebug<MemPoolBehavior>($"Received inventory too big. {nameof(MAX_INV_SIZE)}: {MAX_INV_SIZE}, Node: {node.RemoteSocketEndpoint}");
				return;
			}

			foreach (var inv in payload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
			{
				if (MemPoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry entry)) // If we have the transaction to be broadcasted then broadcast it now.
				{
					if (entry.NodeRemoteSocketEndpoint != node.RemoteSocketEndpoint.ToString())
					{
						continue; // Would be strange. It could be some kind of attack.
					}

					try
					{
						var txPayload = new TxPayload(entry.Transaction);
						if (!node.IsConnected)
						{
							Logger.LogInfo<MemPoolBehavior>($"Couldn't serve transaction. Node ({node.RemoteSocketEndpoint}) is not connected anymore: {entry.TransactionId}.");
						}
						else
						{
							await node.SendMessageAsync(txPayload);
							entry.MakeBroadcasted();
							Logger.LogInfo<MemPoolBehavior>($"Successfully served transaction to node ({node.RemoteSocketEndpoint}): {entry.TransactionId}.");
						}
					}
					catch (Exception ex)
					{
						Logger.LogInfo<MemPoolBehavior>(ex);
					}
				}
			}
		}

		private async Task ProcessInvAsync(Node node, InvPayload payload)
		{
			if (payload.Inventory.Count > MAX_INV_SIZE)
			{
				Logger.LogDebug<MemPoolBehavior>($"Received inventory too big. {nameof(MAX_INV_SIZE)}: {MAX_INV_SIZE}, Node: {node.RemoteSocketEndpoint}");
				return;
			}

			var getDataPayload = new GetDataPayload();
			foreach (var inv in payload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
			{
				if (MemPoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry entry)) // If we have the transaction then adjust confirmation.
				{
					try
					{
						if (entry.NodeRemoteSocketEndpoint == node.RemoteSocketEndpoint.ToString())
						{
							continue; // Wtf, why are you trying to broadcast it back to us?
						}

						entry.ConfirmPropagationOnce();
					}
					catch (Exception ex)
					{
						Logger.LogInfo<MemPoolBehavior>(ex);
					}
				}

				// if we already have it continue;
				if (!MemPoolService.TransactionHashes.TryAdd(inv.Hash))
				{
					continue;
				}

				getDataPayload.Inventory.Add(inv);
			}

			if (getDataPayload.Inventory.Any() && node.IsConnected)
			{
				// ask for the whole transaction
				await node.SendMessageAsync(getDataPayload);
			}
		}

		private void ProcessTx(TxPayload payload)
		{
			Transaction transaction = payload.Object;
			MemPoolService.OnTransactionReceived(new SmartTransaction(transaction, Height.MemPool));
		}

		public override object Clone()
		{
			return new MemPoolBehavior(MemPoolService);
		}
	}
}
