using NBitcoin;
using WalletWasabi.Blockchain.Blocks;
using Xunit;
using static WalletWasabi.Models.Height;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Blocks;

/// <summary>
/// Tests for <see cref="FilterHeaderChain"/>.
/// </summary>
public class FilterHeaderChainTests
{
	private static DateTimeOffset BlockTime { get; } = DateTimeOffset.UtcNow;

	[Fact]
	public void InvalidAddTests()
	{
		var chain = new FilterHeaderChain();
		AssertEverythingDefault(chain);

		// Attempt to remove an element when there is none.
		Assert.False(chain.RemoveTip());
		AssertEverythingDefault(chain);

		var header = CreateGenesisHeader();

		// Add first header.
		Assert.True(chain.TryAppendTip(header));

		// This tip is considered to be old.
		Assert.False(chain.TryAppendTip(header));
	}

	/// <summary>
	/// Attempts to replace an added header.
	/// </summary>
	[Fact]
	public void ReplaceTests()
	{
		var chain = new FilterHeaderChain();

		// Add first header.
		{
			var header = CreateGenesisHeader();
			Assert.True(chain.TryAppendTip(header));
		}

		uint height = 1;

		// Add new header.
		{
			var header = CreateSmartHeader(new uint256(465465465), chain.TipHash!, height);
			Assert.True(chain.TryAppendTip(header));
			Assert.Equal(2, chain.HashCount);
		}

		// Attempt to replace the newly added header.
		{
			var header = CreateSmartHeader(new uint256(778797897), chain.TipHash!, height);

			// Header height isn't one more than the previous header height.
			Assert.False(chain.TryAppendTip(header));
			Assert.Equal(2, chain.HashCount);
		}
	}

	/// <summary>
	/// Adds and removes headers to test that chain does not get corrupted.
	/// </summary>
	[Fact]
	public void AddAndRemoveTests()
	{
		var chain = new FilterHeaderChain();
		AssertEverythingDefault(chain);

		// Attempt to remove an element when there is none.
		Assert.False(chain.RemoveTip());
		AssertEverythingDefault(chain);

		// Add first header.
		{
			var header = CreateGenesisHeader();
			Assert.True(chain.TryAppendTip(header));
		}

		for (uint i = 0; i < 5000; i++)
		{
			uint height = chain.TipHeight + 1;

			// Add a new header.
			{
				var header = CreateSmartHeader(new uint256(height), chain.TipHash!, height);
				Assert.True(chain.TryAppendTip(header));
			}
		}

		for (uint i = 0; i < 3000; i++)
		{
			Assert.True(chain.RemoveTip());
		}

		for (uint i = 0; i < 500; i++)
		{
			uint height = chain.TipHeight + 1;

			// Add a new header.
			{
				var header = CreateSmartHeader(new uint256(height), chain.TipHash!, height);
				Assert.True(chain.TryAppendTip(header));
			}
		}

		Assert.Equal(2500u, chain.Tip!.Height.Height);
	}

	[Fact]
	public void ServerTipHeightTests()
	{
		var chain = new FilterHeaderChain();
		Assert.Equal(ChainHeight.Genesis, chain.ServerTipHeight);

		chain.SetServerTipHeight(2);
		Assert.Equal(2, chain.HashesLeft);

		// Add first header.
		{
			var header = CreateGenesisHeader();
			Assert.True(chain.TryAppendTip(header));
		}

		Assert.Equal(2, chain.HashesLeft);

		// Add second header.
		{
			var header = CreateSmartHeader(new uint256(1), chain.TipHash!, height: 1);
			Assert.True(chain.TryAppendTip(header));
			Assert.Equal(1, chain.HashesLeft);
		}

		// Add third header.
		{
			var header = CreateSmartHeader(new uint256(2), chain.TipHash!, height: 2);
			Assert.True(chain.TryAppendTip(header));
			Assert.Equal(0, chain.HashesLeft);
		}

		// Add fourth header. Hashes left should not report negative numbers.
		{
			var header = CreateSmartHeader(new uint256(3), chain.TipHash!, height: 3);
			Assert.True(chain.TryAppendTip(header));
			Assert.Equal(0, chain.HashesLeft);
		}
	}

	[Fact]
	public void HashCountTests()
	{
		var chain = new FilterHeaderChain();
		Assert.Equal(ChainHeight.Genesis, chain.ServerTipHeight);

		// Add 1st header.
		{
			var header = CreateGenesisHeader();
			Assert.True(chain.TryAppendTip(header));
			Assert.Equal(1, chain.HashCount);
		}

		// Add 2nd header.
		{
			var header = CreateSmartHeader(new uint256(1), chain.TipHash!, height: 1);
			Assert.True(chain.TryAppendTip(header));
			Assert.Equal(2, chain.HashCount);
		}

		// Add 3rd header.
		{
			var header = CreateSmartHeader(new uint256(2), chain.TipHash!, height: 2);
			Assert.True(chain.TryAppendTip(header));
			Assert.Equal(3, chain.HashCount);
		}

		// Add 4th header.
		{
			var header = CreateSmartHeader(new uint256(3), chain.TipHash!, height: 3);
			Assert.True(chain.TryAppendTip(header));
			Assert.Equal(4, chain.HashCount);
		}
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

	private static void AssertEverythingDefault(FilterHeaderChain chain)
	{
		Assert.Equal(0, chain.HashCount);
		Assert.Equal(0, chain.HashesLeft);
		Assert.Equal(ChainHeight.Genesis, chain.ServerTipHeight);
		Assert.Null(chain.TipHash);
		Assert.Equal(ChainHeight.Genesis, chain.TipHeight);
	}
}
