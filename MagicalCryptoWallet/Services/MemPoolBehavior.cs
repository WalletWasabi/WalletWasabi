using ConcurrentCollections;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.Services
{
    public class MemPoolBehavior : NodeBehavior
	{
		const int MAX_INV_SIZE = 50000;

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
				return;
			}
			catch (Exception ex)
			{
				Logger.LogInfo<MemPoolBehavior>($"Ignoring {ex.GetType()}: {ex.Message}");
				Logger.LogDebug<MemPoolBehavior>(ex);
				return;
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
				if(!MemPoolService.TransactionHashes.Add(inv.Hash))
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
			var transaction = transactionPayload.Object as Transaction;
			MemPoolService.OnTransactionReceived(new SmartTransaction(transaction, Height.Unknown));
		}

		public override object Clone()
		{
			return new MemPoolBehavior(MemPoolService);
		}
	}
}
