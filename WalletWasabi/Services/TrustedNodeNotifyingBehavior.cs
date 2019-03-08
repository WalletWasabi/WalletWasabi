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
					foreach (var inv in invPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX) || inv.Type.HasFlag(InventoryType.MSG_BLOCK)))
					{
						payload.Inventory.Add(inv);
					}

					if (node.IsConnected)
					{
						// ask for the whole transaction
						await node.SendMessageAsync(payload);
					}
				}
				else if (message.Message.Payload is TxPayload txPayload)
				{
					Transaction?.Invoke(this, txPayload.Object);
				}
				else if(message.Message.Payload is BlockPayload blockPayload)
				{
					Block?.Invoke(this, blockPayload.Object);
				}
			}
			catch (OperationCanceledException ex)
			{
				Logger.LogDebug<TrustedNodeNotifyingBehavior>(ex);
			}
			catch (Exception ex)
			{
				Logger.LogInfo<TrustedNodeNotifyingBehavior>($"Ignoring {ex.GetType()}: {ex.Message}");
				Logger.LogDebug<TrustedNodeNotifyingBehavior>(ex);
			}
		}

		public override object Clone()
		{
			return new TrustedNodeNotifyingBehavior();
		}
	}
}
