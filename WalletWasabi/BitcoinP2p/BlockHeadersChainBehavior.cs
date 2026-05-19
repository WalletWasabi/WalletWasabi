using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinP2p;

public class BlockHeadersChainBehavior(
	ConcurrentChain blockHeaderChain,
	FilterHeaderChain filterHeaderChain,
	EventBus eventBus)
	: ChainBehavior(blockHeaderChain)
{
	protected override void AttachCore()
	{
		base.AttachCore();
		AttachedNode.StateChanged += AttachedNodeOnStateChanged;
	}

	protected override void DetachCore()
	{
		AttachedNode.StateChanged -= AttachedNodeOnStateChanged;
		base.DetachCore();
	}

	private void AttachedNodeOnStateChanged(Node node, NodeState oldState)
	{
		if (node.State == NodeState.HandShaked)
		{
			var myBestFilterHeight = filterHeaderChain.ServerTipHeight;
			var theirBestFilterHeight = AttachedNode.PeerVersion.StartHeight;
			if (theirBestFilterHeight > myBestFilterHeight)
			{
				filterHeaderChain.SetServerTipHeight((uint)theirBestFilterHeight);
				eventBus.Publish(new NetworkTipHeightChanged((uint)theirBestFilterHeight));
			}
		}
	}

	public override object Clone()
	{
		return new BlockHeadersChainBehavior(Chain, filterHeaderChain, eventBus);
	}
}
