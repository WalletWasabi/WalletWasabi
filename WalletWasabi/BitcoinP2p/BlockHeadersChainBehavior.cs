using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinP2p;

public class BlockHeadersChainBehavior(
	ConcurrentChain blockHeaderChain,
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
		return new BlockHeadersChainBehavior(Chain, eventBus);
	}
}
