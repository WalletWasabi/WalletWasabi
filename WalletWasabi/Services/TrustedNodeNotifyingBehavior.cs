using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Services
{
	public class TrustedNodeNotifyingBehavior : NodeBehavior
	{
		public event EventHandler<uint256> BlockInv;

		public MempoolService MempoolService { get; }

		public TrustedNodeNotifyingBehavior(MempoolService mempoolService)
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
				if (message.Message.Payload is TxPayload txPayload)
				{
					ProcessTx(txPayload);
				}
				else if (message.Message.Payload is InvPayload invPayload)
				{
					await ProcessInvAsync(node, invPayload).ConfigureAwait(false);
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

		private async Task ProcessInvAsync(Node node, InvPayload invPayload)
		{
			var getDataPayload = new GetDataPayload();
			foreach (var inv in invPayload.Inventory)
			{
				if (inv.Type.HasFlag(InventoryType.MSG_TX))
				{
					if (MempoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry entry)) // If we have the transaction then adjust confirmation.
					{
						try
						{
							if (entry.NodeRemoteSocketEndpoint == node.RemoteSocketEndpoint.ToString())
							{
								continue; // Wtf, why are you trying to broadcast it back to us?
							}

							entry.MakeBroadcasted();
							entry.ConfirmPropagationForGood();
						}
						catch (Exception ex)
						{
							Logger.LogInfo(ex);
						}
					}

					// if we already processed it continue;
					if (MempoolService.IsProcessed(inv.Hash))
					{
						continue;
					}

					getDataPayload.Inventory.Add(inv);
				}

				if (inv.Type.HasFlag(InventoryType.MSG_BLOCK))
				{
					BlockInv?.Invoke(this, inv.Hash);
				}
			}

			if (getDataPayload.Inventory.Any() && node.IsConnected)
			{
				// ask for the whole transaction
				await node.SendMessageAsync(getDataPayload).ConfigureAwait(false);
			}
		}

		private void ProcessTx(TxPayload payload)
		{
			Transaction transaction = payload.Object;
			transaction.PrecomputeHash(false, true);
			MempoolService.Process(transaction);
		}

		public override object Clone()
		{
			// Note that, this is not clone! So this must be used after we are connected to a node, because it'll have as many clones as nodes.
			return new TrustedNodeNotifyingBehavior(MempoolService);
		}
	}
}
