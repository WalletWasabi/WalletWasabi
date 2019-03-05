using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace WalletWasabi
{
	public class SyncBehavior : NodeBehavior
	{
		private uint256 _latestKnownHash = null;
		private bool _isSynchronized = false;
		private HashSet<uint256> _blockInventory = new HashSet<uint256>();
		public EventHandler<EventArgs> Synchronized;

		public void UpdateKnowTip(uint256 blockHash)
		{
			_latestKnownHash = blockHash;
			CheckSynchronization();
		}

		protected override void AttachCore()
		{
			AttachedNode.MessageReceived += AttachedNode_MessageReceived;
		}

		private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
		{
			if(/*_isSynchronized ||*/ _latestKnownHash == null) return;

			var inv = message.Message.Payload as InvPayload;
			if(inv != null)
			{
				foreach(var item in inv.Inventory)
				{
					if((item.Type & InventoryType.MSG_BLOCK) == InventoryType.MSG_BLOCK)
					{
						_blockInventory.Add(item.Hash);
						CheckSynchronization();
					}
				}
			}
		}

		private void CheckSynchronization()
		{
			if(_blockInventory.Contains(_latestKnownHash))
			{
				try
				{
					var syncEvent = Synchronized;
					if(syncEvent != null)
						syncEvent(this, EventArgs.Empty);
				}
				finally
				{
					_isSynchronized = true;
				}
			}
		}

		public override object Clone()
		{
			return this;
		}

		protected override void DetachCore()
		{
			AttachedNode.MessageReceived -= AttachedNode_MessageReceived;
		}
	}
}