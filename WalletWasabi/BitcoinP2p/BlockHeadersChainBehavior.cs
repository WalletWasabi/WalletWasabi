using NBitcoin;
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
		var myBestFilterHeight = filterHeaderChain.ServerTipHeight;
		var theirBestFilterHeight = AttachedNode.MyVersion.StartHeight;
		if (theirBestFilterHeight > myBestFilterHeight)
		{
			filterHeaderChain.SetServerTipHeight((uint)theirBestFilterHeight);
			eventBus.Publish(new ServerTipHeightChanged((uint)theirBestFilterHeight));
		}
	}

	public override object Clone()
	{
		return new BlockHeadersChainBehavior(Chain, filterHeaderChain, eventBus);
	}
}
