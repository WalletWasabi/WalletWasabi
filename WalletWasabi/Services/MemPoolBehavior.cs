using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Linq;
using System.Threading.Tasks;

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
				if (message.Message.Payload is TxPayload txPayload)
				{
					ProcessTxPayload(txPayload);
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

		private async Task ProcessInvAsync(Node node, InvPayload invPayload)
		{
			if (invPayload.Inventory.Count > MAX_INV_SIZE)
			{
				Logger.LogDebug($"Received inventory too big. {nameof(MAX_INV_SIZE)}: {MAX_INV_SIZE}, Node: {node.RemoteSocketEndpoint}");
				return;
			}

			var payload = new GetDataPayload();
			foreach (var inv in invPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
			{
				// if we already have it continue;
				if (!MemPoolService.TransactionHashes.TryAdd(inv.Hash))
				{
					continue;
				}

				payload.Inventory.Add(inv);
			}

			if (node.IsConnected)
			{
				// ask for the whole transaction
				await node.SendMessageAsync(payload);
			}
		}

		private void ProcessTxPayload(TxPayload transactionPayload)
		{
			Transaction transaction = transactionPayload.Object;
			MemPoolService.OnTransactionReceived(new SmartTransaction(transaction, Height.MemPool));
		}

		public override object Clone()
		{
			return new MemPoolBehavior(MemPoolService);
		}
	}
}
