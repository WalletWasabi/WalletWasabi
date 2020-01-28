using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.P2p
{
	public class TrustedP2pBehavior : P2pBehavior
	{
		public event EventHandler<uint256> BlockInv;

		public TrustedP2pBehavior(MempoolService mempoolService) : base(mempoolService)
		{
		}

		protected override bool ProcessInventoryVector(InventoryVector inv, EndPoint remoteSocketEndpoint)
		{
			if (inv.Type.HasFlag(InventoryType.MSG_TX))
			{
				if (MempoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry entry)) // If we have the transaction then adjust confirmation.
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

		public override object Clone() => new TrustedP2pBehavior(MempoolService);
	}
}
