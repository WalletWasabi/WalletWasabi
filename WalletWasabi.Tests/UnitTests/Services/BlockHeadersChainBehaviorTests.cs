using System;
using System.Linq;
using NBitcoin;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Tests.UnitTests.Blockchain;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class BlockHeadersChainBehaviorTests
{
	[Fact]
	public void LocatorMatchesNBitcoinForAGenesisRootedChain()
	{
		var chain = new ConcurrentChain(Network.RegTest);
		TestChain.Extend(chain, 40);

		var locator = BlockHeadersChainBehavior.CreateLocator(chain.Tip);

		Assert.Equal(chain.Tip.GetLocator().Blocks, locator.Blocks);
	}

	[Fact]
	public void LocatorStopsAtACheckpointRoot()
	{
		var chain = TestChain.CreateRootedAt(100);
		TestChain.Extend(chain, 3);

		// NBitcoin's locator walks past the root; that is why the behavior builds its own.
		Assert.Throws<NullReferenceException>(() => chain.Tip.GetLocator());

		var locator = BlockHeadersChainBehavior.CreateLocator(chain.Tip);

		Assert.Equal([chain.GetBlock(103).HashBlock, chain.GetBlock(102).HashBlock, chain.GetBlock(101).HashBlock, chain.GetBlock(100).HashBlock], locator.Blocks);
	}

	[Fact]
	public void LocatorAlwaysEndsWithTheCheckpointRootWhenStepsGrow()
	{
		// With more than 10 entries the step doubles, so a naive walk overshoots the root and leaves it out.
		var chain = TestChain.CreateRootedAt(100);
		TestChain.Extend(chain, 14);

		var locator = BlockHeadersChainBehavior.CreateLocator(chain.Tip);

		var heights = locator.Blocks.Select(h => chain.GetBlock(h).Height).ToArray();
		Assert.Equal(heights.OrderByDescending(h => h), heights);
		Assert.Equal(114, heights[0]);
		Assert.Equal(100, heights[^1]);
		Assert.Equal(heights.Distinct().Count(), heights.Length);
		Assert.Equal([114, 113, 112, 111, 110, 109, 108, 107, 106, 105, 104, 103, 101, 100], heights);
	}

	[Fact]
	public void LocatorOfARootOnlyChainContainsJustTheRoot()
	{
		var chain = TestChain.CreateRootedAt(969696);

		var locator = BlockHeadersChainBehavior.CreateLocator(chain.Tip);

		Assert.Equal([chain.Tip.HashBlock], locator.Blocks);
	}

	[Fact]
	public void ConnectsValidHeadersOnTopOfACheckpointRoot()
	{
		var chain = TestChain.CreateRootedAt(100);
		var a = TestChain.Mine(chain.Tip);
		var b = TestChain.Mine(a);
		var c = TestChain.Mine(b);

		var (tip, invalid) = BlockHeadersChainBehavior.ConnectHeaders(chain.Tip, [a.Header, b.Header, c.Header], chain, Network.RegTest);

		Assert.Null(invalid);
		Assert.Equal(103, tip.Height);
		Assert.Equal(c.HashBlock, tip.HashBlock);
		Assert.Equal(chain.Tip, tip.FindAncestorOrSelf(chain.Tip.HashBlock));
	}

	[Fact]
	public void StopsAtTheFirstHeaderWithWrongDifficulty()
	{
		var chain = TestChain.CreateRootedAt(100);
		var a = TestChain.Mine(chain.Tip);
		var wrongBits = TestChain.Mine(a, bits: new Target(0x207ffffe));
		var c = TestChain.Mine(wrongBits);

		var (tip, invalid) = BlockHeadersChainBehavior.ConnectHeaders(chain.Tip, [a.Header, wrongBits.Header, c.Header], chain, Network.RegTest);

		Assert.NotNull(invalid);
		Assert.Equal(wrongBits.HashBlock, invalid.HashBlock);
		Assert.Equal(a.HashBlock, tip.HashBlock);
	}

	[Fact]
	public void IgnoresHeadersThatDoNotConnect()
	{
		var chain = TestChain.CreateRootedAt(100);
		var other = TestChain.CreateRootedAt(100);
		var unrelated = TestChain.Mine(TestChain.Mine(other.Tip));

		var (tip, invalid) = BlockHeadersChainBehavior.ConnectHeaders(chain.Tip, [unrelated.Header], chain, Network.RegTest);

		Assert.Null(invalid);
		Assert.Equal(chain.Tip, tip);
	}
}
