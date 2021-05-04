using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Net;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.BitcoinP2p
{
	public class TrustedP2pBehavior : P2pBehavior
	{
		public TrustedP2pBehavior(MempoolService mempoolService) : base(mempoolService)
		{
		}

		public event EventHandler<uint256>? BlockInv;

		private HashSet<uint256> DisabledFromProcessing { get; } = new();
		private object DisabledFromProcessingLock { get; } = new();

		protected override bool ProcessInventoryVector(InventoryVector inv, EndPoint remoteSocketEndpoint)
		{
			if (inv.Type.HasFlag(InventoryType.MSG_TX))
			{
				if (MempoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry? entry)) // If we have the transaction then adjust confirmation.
				{
					if (entry.NodeRemoteSocketEndpoint == remoteSocketEndpoint.ToString())
					{
						return false; // Wtf, why are you trying to broadcast it back to us?
					}

					entry.ConfirmPropagationForGood();
				}

				// If we already processed it continue.
				if (MempoolService.IsProcessed(inv.Hash))
				{
					return false;
				}

				return true;
			}

			if (inv.Type.HasFlag(InventoryType.MSG_BLOCK))
			{
				BlockInv?.Invoke(this, inv.Hash);
			}

			return false;
		}

		protected override void ProcessTx(TxPayload payload)
		{
			var txid = payload.Object.GetHash();
			bool toProcess;

			lock (DisabledFromProcessingLock)
			{
				toProcess = !DisabledFromProcessing.Contains(txid);
			}

			if (toProcess)
			{
				base.ProcessTx(payload);
			}
		}

		public override object Clone() => new TrustedP2pBehavior(MempoolService);

		public void DisableProcess(IEnumerable<uint256> txids)
		{
			lock (DisabledFromProcessingLock)
			{
				foreach (var txid in txids)
				{
					DisabledFromProcessing.Add(txid);
				}
			}
		}

		public void EnableProcess(IEnumerable<uint256> txids)
		{
			lock (DisabledFromProcessingLock)
			{
				foreach (var txid in txids)
				{
					DisabledFromProcessing.Remove(txid);
				}
			}
		}
	}
}
