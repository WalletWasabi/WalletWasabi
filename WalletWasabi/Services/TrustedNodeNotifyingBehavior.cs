using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class TrustedNodeNotifyingBehavior : NodeBehavior
	{
		public event EventHandler<uint256> TransactionInv;

		public event EventHandler<uint256> BlockInv;

		public event EventHandler<Transaction> Transaction;

		public event EventHandler<Block> Block;

		public TrustedNodeNotifyingBehavior()
		{
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
				if (message.Message.Payload is InvPayload invPayload)
				{
					var payload = new GetDataPayload();
					foreach (var inv in invPayload.Inventory)
					{
						if (inv.Type.HasFlag(InventoryType.MSG_TX))
						{
							TransactionInv?.Invoke(this, inv.Hash);
							payload.Inventory.Add(inv);
						}
						else if (inv.Type.HasFlag(InventoryType.MSG_BLOCK))
						{
							BlockInv?.Invoke(this, inv.Hash);
							payload.Inventory.Add(inv);
						}
					}

					if (payload.Inventory.Any() && node.IsConnected)
					{
						// ask for the whole transaction
						await node.SendMessageAsync(payload);
					}
				}
				else if (message.Message.Payload is TxPayload txPayload)
				{
					Transaction?.Invoke(this, txPayload.Object);
				}
				else if (message.Message.Payload is BlockPayload blockPayload)
				{
					Block?.Invoke(this, blockPayload.Object);
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

		public override object Clone()
		{
			// Note that, this is not clone! So this must be used after we are connected to a node, because it'll have as many clones as nodes.
			return new TrustedNodeNotifyingBehavior();
		}
	}
}
