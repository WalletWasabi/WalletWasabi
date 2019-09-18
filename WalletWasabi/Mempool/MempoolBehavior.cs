using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Mempool
{
	public class MempoolBehavior : NodeBehavior
	{
		private const int MaxInvSize = 50000;

		public MempoolService MempoolService { get; }

		public MempoolBehavior(MempoolService mempoolService)
		{
			MempoolService = Guard.NotNull(nameof(mempoolService), mempoolService);
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
				Logger.LogDebug(ex);
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"Ignoring {ex.GetType()}: {ex.Message}");
				Logger.LogDebug(ex);
			}
		}

		private async Task ProcessGetDataAsync(Node node, GetDataPayload payload)
		{
			if (payload.Inventory.Count > MaxInvSize)
			{
				Logger.LogDebug($"Received inventory too big. {nameof(MaxInvSize)}: {MaxInvSize}, Node: {node.RemoteSocketEndpoint}");
				return;
			}

			foreach (var inv in payload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
			{
				if (MempoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry entry)) // If we have the transaction to be broadcasted then broadcast it now.
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
							Logger.LogInfo($"Could not serve transaction. Node ({node.RemoteSocketEndpoint}) is not connected anymore: {entry.TransactionId}.");
						}
						else
						{
							await node.SendMessageAsync(txPayload);
							entry.MakeBroadcasted();
							Logger.LogInfo($"Successfully served transaction to node ({node.RemoteSocketEndpoint}): {entry.TransactionId}.");
						}
					}
					catch (Exception ex)
					{
						Logger.LogInfo(ex);
					}
				}
			}
		}

		private async Task ProcessInvAsync(Node node, InvPayload payload)
		{
			if (payload.Inventory.Count > MaxInvSize)
			{
				Logger.LogDebug($"Received inventory too big. {nameof(MaxInvSize)}: {MaxInvSize}, Node: {node.RemoteSocketEndpoint}");
				return;
			}

			var getDataPayload = new GetDataPayload();
			foreach (var inv in payload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
			{
				if (MempoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry entry)) // If we have the transaction then adjust confirmation.
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
						Logger.LogInfo(ex);
					}
				}

				// if we already have it continue;
				if (!MempoolService.TransactionHashes.TryAdd(inv.Hash))
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
			MempoolService.OnTransactionReceived(new SmartTransaction(transaction, Height.Mempool));
		}

		public override object Clone()
		{
			return new MempoolBehavior(MempoolService);
		}
	}
}
