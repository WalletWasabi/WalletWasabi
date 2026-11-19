using NBitcoin;
using WalletWasabi.BitcoinP2p;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

/// <summary>
/// Tests for <see cref="BlockHeaderSynchronizationState"/>.
/// </summary>
public class BlockHeaderSynchronizationStateTests
{
	private static readonly Network Network = Network.RegTest;

	/// <summary>
	/// A node that keeps filling its messages has more headers. The first message that is not full ends the download.
	/// </summary>
	[Fact]
	public void SynchronizedWhenTheNodeHasNoMoreHeaders()
	{
		var chain = CreateChain(height: 4500);
		var state = new BlockHeaderSynchronizationState();
		Assert.False(state.IsSynchronized);

		state.OnHeadersProcessed(isFullMessage: true, hasExtendedChain: true, chainTip: chain.GetBlock(2000), nodeTip: chain.GetBlock(2000));
		Assert.False(state.IsSynchronized);

		state.OnHeadersProcessed(isFullMessage: true, hasExtendedChain: true, chainTip: chain.GetBlock(4000), nodeTip: chain.GetBlock(4000));
		Assert.False(state.IsSynchronized);

		state.OnHeadersProcessed(isFullMessage: false, hasExtendedChain: true, chainTip: chain.GetBlock(4500), nodeTip: chain.GetBlock(4500));
		Assert.True(state.IsSynchronized);
	}

	/// <summary>
	/// The chain that was stored on the last run can be at the tip already. The node says so with an empty message.
	/// </summary>
	[Fact]
	public void SynchronizedWhenTheNodeHasNothingNew()
	{
		var chain = CreateChain(height: 10);
		var state = new BlockHeaderSynchronizationState();

		state.OnHeadersProcessed(isFullMessage: false, hasExtendedChain: false, chainTip: chain.Tip, nodeTip: chain.Tip);

		Assert.True(state.IsSynchronized);
	}

	/// <summary>
	/// A node whose chain is shorter says nothing about the tip of the network.
	/// </summary>
	[Fact]
	public void NodeBehindTheChainDoesNotSynchronize()
	{
		var chain = CreateChain(height: 200);
		var state = new BlockHeaderSynchronizationState();

		state.OnHeadersProcessed(isFullMessage: false, hasExtendedChain: false, chainTip: chain.Tip, nodeTip: chain.GetBlock(100));

		Assert.False(state.IsSynchronized);
	}

	/// <summary>
	/// A node on another block of the same height has nothing better than the chain either.
	/// </summary>
	[Fact]
	public void NodeOnCompetingTipSynchronizes()
	{
		var chain = CreateChain(height: 10);
		var competingTip = CreateBlock(chain.GetBlock(9), nonce: 12345);
		var state = new BlockHeaderSynchronizationState();

		state.OnHeadersProcessed(isFullMessage: false, hasExtendedChain: false, chainTip: chain.Tip, nodeTip: competingTip);

		Assert.True(state.IsSynchronized);
	}

	/// <summary>
	/// After a long time offline the chain is far behind again, which shows as a full message that extends it.
	/// </summary>
	[Fact]
	public void FullMessageThatExtendsTheChainUnsynchronizes()
	{
		var chain = CreateChain(height: 2010);
		var state = new BlockHeaderSynchronizationState();
		state.OnHeadersProcessed(isFullMessage: false, hasExtendedChain: false, chainTip: chain.GetBlock(10), nodeTip: chain.GetBlock(10));
		Assert.True(state.IsSynchronized);

		state.OnHeadersProcessed(isFullMessage: true, hasExtendedChain: true, chainTip: chain.Tip, nodeTip: chain.Tip);

		Assert.False(state.IsSynchronized);
	}

	/// <summary>
	/// Two nodes that are asked for the same headers send the same full message. Only the first one extends the chain,
	/// the other one tells nothing new.
	/// </summary>
	[Fact]
	public void FullMessageOfKnownHeadersChangesNothing()
	{
		var chain = CreateChain(height: 4000);
		var state = new BlockHeaderSynchronizationState();

		// Nothing is known yet, and a message that brings nothing does not tell more.
		state.OnHeadersProcessed(isFullMessage: true, hasExtendedChain: false, chainTip: chain.GetBlock(2000), nodeTip: chain.GetBlock(2000));
		Assert.False(state.IsSynchronized);

		state.OnHeadersProcessed(isFullMessage: false, hasExtendedChain: false, chainTip: chain.Tip, nodeTip: chain.Tip);
		Assert.True(state.IsSynchronized);

		// The chain is at the tip, and a node that repeats what the chain has does not change that.
		state.OnHeadersProcessed(isFullMessage: true, hasExtendedChain: false, chainTip: chain.Tip, nodeTip: chain.Tip);
		Assert.True(state.IsSynchronized);
	}

	/// <summary>
	/// A new block comes in a message of one header. The block headers stay synchronized.
	/// </summary>
	[Fact]
	public void NewBlockKeepsTheChainSynchronized()
	{
		var chain = CreateChain(height: 11);
		var state = new BlockHeaderSynchronizationState();
		state.OnHeadersProcessed(isFullMessage: false, hasExtendedChain: false, chainTip: chain.GetBlock(10), nodeTip: chain.GetBlock(10));

		state.OnHeadersProcessed(isFullMessage: false, hasExtendedChain: true, chainTip: chain.Tip, nodeTip: chain.Tip);

		Assert.True(state.IsSynchronized);
	}

	private static ConcurrentChain CreateChain(int height)
	{
		var chain = new ConcurrentChain(Network);
		var tip = chain.Tip;
		for (var i = 0; i < height; i++)
		{
			tip = CreateBlock(tip, nonce: (uint)(tip.Height + 1));
		}

		chain.SetTip(tip);
		return chain;
	}

	private static ChainedBlock CreateBlock(ChainedBlock previous, uint nonce)
	{
		var header = Network.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = previous.HashBlock;
		header.Nonce = nonce;
		return new ChainedBlock(header, header.GetHash(), previous);
	}
}
