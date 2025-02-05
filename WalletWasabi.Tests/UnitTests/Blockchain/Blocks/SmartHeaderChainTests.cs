using NBitcoin;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Blocks;

/// <summary>
/// Tests for <see cref="SmartHeaderChain"/>.
/// </summary>
public class SmartHeaderChainTests
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

		SmartHeader header = CreateGenesisHeader();
		chain.AppendTip(header);

		InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => chain.AppendTip(header));
		Assert.StartsWith("Header height isn't one more than the previous header height.", ex.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Attempts to replace an added header.
	/// </summary>
	[Fact]
	public void ReplaceTests()
	{
		SmartHeaderChain chain = new();

		SmartHeader header = CreateGenesisHeader();
		chain.AppendTip(header);

		uint height = 1;

		// Add new header.
		header = CreateSmartHeader(new uint256(465465465), chain.TipHash!, height);
		chain.AppendTip(header);

		// Attempt to replace the newly added header.
		header = CreateSmartHeader(new uint256(778797897), chain.TipHash!, height);

		InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => chain.AppendTip(header));
		Assert.StartsWith("Header height isn't one more than the previous header height.", ex.Message, StringComparison.Ordinal);
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

		SmartHeader header = CreateGenesisHeader();
		chain.AppendTip(header);

		for (uint i = 0; i < 5000; i++)
		{
			uint height = chain.TipHeight + 1;
			header = CreateSmartHeader(new uint256(height), chain.TipHash!, height);
			chain.AppendTip(header);
		}

		for (uint i = 0; i < 3000; i++)
		{
			Assert.True(chain.RemoveTip());
		}

		for (uint i = 0; i < 500; i++)
		{
			uint height = chain.TipHeight + 1;
			header = CreateSmartHeader(new uint256(height), chain.TipHash!, height);
			chain.AppendTip(header);
		}

		Assert.Equal(2500u, chain.Tip!.Height);
	}

	[Fact]
	public void ServerTipHeightTests()
	{
		SmartHeaderChain chain = new();
		Assert.Equal(0u, chain.ServerTipHeight);

		chain.SetServerTipHeight(2);
		Assert.Equal(2, chain.HashesLeft);

		// Add first header.
		SmartHeader header = CreateGenesisHeader();
		chain.AppendTip(header);
		Assert.Equal(2, chain.HashesLeft);

		// Add second header.
		header = CreateSmartHeader(new uint256(1), chain.TipHash!, height: 1);
		chain.AppendTip(header);
		Assert.Equal(1, chain.HashesLeft);

		// Add third header.
		header = CreateSmartHeader(new uint256(2), chain.TipHash!, height: 2);
		chain.AppendTip(header);
		Assert.Equal(0, chain.HashesLeft);

		// Add fourth header. Hashes left should not report negative numbers
		header = CreateSmartHeader(new uint256(3), chain.TipHash!, height: 3);
		chain.AppendTip(header);
		Assert.Equal(0, chain.HashesLeft);
	}

	[Fact]
	public void HashCountTests()
	{
		SmartHeaderChain chain = new(maxChainSize: 2);
		Assert.Equal(0u, chain.ServerTipHeight);

		// Add 1st header.
		SmartHeader header = CreateGenesisHeader();
		chain.AppendTip(header);
		Assert.Equal(1, chain.HashCount);

		// Add 2nd header.
		header = CreateSmartHeader(new uint256(1), chain.TipHash!, height: 1);
		chain.AppendTip(header);
		Assert.Equal(2, chain.HashCount);

		// Add 3rd header.
		header = CreateSmartHeader(new uint256(2), chain.TipHash!, height: 2);
		chain.AppendTip(header);
		Assert.Equal(3, chain.HashCount);

		// Add 4th header.
		header = CreateSmartHeader(new uint256(3), chain.TipHash!, height: 3);
		chain.AppendTip(header);
		Assert.Equal(4, chain.HashCount);
	}

	/// <remarks>Dummy genesis header.</remarks>
	private static SmartHeader CreateGenesisHeader()
	{
		return new(new uint256("000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f"), header: uint256.Zero, height: 0, BlockTime);
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
