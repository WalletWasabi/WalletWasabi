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
		AttachedNode.MessageReceived += AttachedNodeOnMessageReceived;
		_lastPublishedHeight = Chain.Tip?.Height ?? 0;
	}

	protected override void DetachCore()
	{
		AttachedNode.MessageReceived -= AttachedNodeOnMessageReceived;
		base.DetachCore();
	}

	private void AttachedNodeOnMessageReceived(Node node, IncomingMessage message)
	{
		// At this point, (valid) headers are already processed by the base ChainBehavior.
		if (message.Message.Payload is HeadersPayload && Chain.Tip is { } tip)
		{
			var currentHeight = tip.Height;
			if (currentHeight > _lastPublishedHeight)
			{
				_lastPublishedHeight = currentHeight;
				eventBus.Publish(new BlockHeadersTipChanged((uint)currentHeight));
			}

			if (currentHeight > filterHeaderChain.ServerTipHeight)
			{
				filterHeaderChain.SetServerTipHeight((uint)currentHeight);
				eventBus.Publish(new NetworkTipHeightChanged((uint)currentHeight));
			}
		}
	}

	public override object Clone()
	{
		return new BlockHeadersChainBehavior(Chain, filterHeaderChain, eventBus);
	}
}
