using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Blockchain.P2p
{
	public abstract class P2pBehavior : NodeBehavior
	{
		private const int MaxInvSize = 50000;

		public MempoolService MempoolService { get; }

		protected P2pBehavior(MempoolService mempoolService)
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
					await ProcessGetDataAsync(node, getDataPayload).ConfigureAwait(false);
				}
				if (message.Message.Payload is TxPayload txPayload)
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
					getDataPayload.Inventory.Add(inv);
				}
			}
			if (getDataPayload.Inventory.Any() && node.IsConnected)
			{
				await node.SendMessageAsync(getDataPayload).ConfigureAwait(false);
			}
		}

		protected abstract bool ProcessInventoryVector(InventoryVector inv, EndPoint remoteSocketEndpoint);

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
							await node.SendMessageAsync(txPayload).ConfigureAwait(false);
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

		protected virtual void ProcessTx(TxPayload payload)
		{
			Transaction transaction = payload.Object;
			transaction.PrecomputeHash(false, true);
			MempoolService.Process(transaction);
		}
	}
}
