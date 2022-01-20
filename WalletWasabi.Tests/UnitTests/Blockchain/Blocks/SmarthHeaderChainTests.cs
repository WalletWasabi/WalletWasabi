using NBitcoin;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Blocks;

/// <summary>
/// Tests for <see cref="SmartHeaderChain"/>.
/// </summary>
public class SmarthHeaderChainTests
{
	private static DateTimeOffset BlockTime { get; } = DateTimeOffset.UtcNow;

	[Fact]
	public void InvalidAddTests()
	{
		SmartHeaderChain chain = new();
		AssertEverythingDefault(chain);

		// Attempt to remove an element when there is none.
		Assert.False(chain.RemoveTip());
		AssertEverythingDefault(chain);

		uint height = 0;
		SmartHeader header = CreateSmartHeader(new uint256("000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f"), prevHash: uint256.Zero, height);
		chain.AddOrReplace(header);

		InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => chain.AddOrReplace(header));
		Assert.StartsWith("Header doesn't point to previous header.", ex.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Adds and removes headers to test that chain does not get corrupted.
	/// </summary>
	[Fact]
	public void AddAndRemoveTests()
	{
		SmartHeaderChain chain = new();
		AssertEverythingDefault(chain);

		// Attempt to remove an element when there is none.
		Assert.False(chain.RemoveTip());
		AssertEverythingDefault(chain);

		uint newServerHeight = chain.ServerTipHeight + 1;
		chain.UpdateServerTipHeight(newServerHeight);
		SmartHeader header = new(new uint256("000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f"), prevHash: uint256.Zero, height: 0, BlockTime);
		chain.AddOrReplace(header);

		for (uint i = 0; i < 5000; i++)
		{
			uint height = chain.TipHeight + 1;
			header = CreateSmartHeader(new uint256(height), chain.TipHash!, height);
			chain.AddOrReplace(header);
		}

		for (uint i = 0; i < 3000; i++)
		{
			Assert.True(chain.RemoveTip());
		}

		for (uint i = 0; i < 500; i++)
		{
			uint height = chain.TipHeight + 1;
			header = CreateSmartHeader(new uint256(height), chain.TipHash!, height);
			chain.AddOrReplace(header);
		}

		Assert.Equal(2500u, chain.Tip!.Height);
		Assert.Equal(2500u, chain.Tip!.Height);
	}

	private static SmartHeader CreateSmartHeader(uint256 blockHash, uint256 prevHash, uint height)
	{
		return new SmartHeader(blockHash, prevHash, height, BlockTime);
	}

	private static void AssertEverythingDefault(SmartHeaderChain chain)
	{
		Assert.Equal(0, chain.HashCount);
		Assert.Equal(0, chain.HashesLeft);
		Assert.Equal(0u, chain.ServerTipHeight);
		Assert.Null(chain.TipHash);
		Assert.Equal(0u, chain.TipHeight);
	}
}
