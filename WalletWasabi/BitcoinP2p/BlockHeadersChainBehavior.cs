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
	private int _lastPublishedHeight;

	protected override void AttachCore()
	{
		base.AttachCore();
		AttachedNode.StateChanged += AttachedNodeOnStateChanged;
		AttachedNode.MessageReceived += AttachedNodeOnMessageReceived;
		_lastPublishedHeight = Chain.Tip?.Height ?? 0;
	}

	protected override void DetachCore()
	{
		AttachedNode.StateChanged -= AttachedNodeOnStateChanged;
		AttachedNode.MessageReceived -= AttachedNodeOnMessageReceived;
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

	private void AttachedNodeOnMessageReceived(Node node, IncomingMessage message)
	{
		if (message.Message.Payload is HeadersPayload)
		{
			var currentHeight = Chain.Tip?.Height ?? 0;
			if (currentHeight > _lastPublishedHeight)
			{
				_lastPublishedHeight = currentHeight;
				eventBus.Publish(new BlockHeadersTipChanged((uint)currentHeight));
			}
		}
	}

	public override object Clone()
	{
		return new BlockHeadersChainBehavior(Chain, filterHeaderChain, eventBus);
	}
}
