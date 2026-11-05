using System;
using System.Linq;
using System.IO;
using NBitcoin;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain;

public class BlockHeaderChainFileTests
{
	private static readonly Network Network = Network.RegTest;

	[Fact]
	public void RoundTripsAGenesisRootedChain()
	{
		var chain = new ConcurrentChain(Network);
		TestChain.Extend(chain, 5);

		var loaded = BlockHeaderChainFile.Deserialize(BlockHeaderChainFile.Serialize(chain), Network);

		Assert.Equal(0u, BlockHeaderChainFile.GetRootHeight(loaded));
		Assert.Equal(chain.Tip.HashBlock, loaded.Tip.HashBlock);
		Assert.Equal(5, loaded.Tip.Height);
		Assert.Equal(chain.GetBlock(3).HashBlock, loaded.GetBlock(3).HashBlock);
		Assert.Equal(Network.GenesisHash, loaded.GetBlock(0).HashBlock);
	}

	[Fact]
	public void RoundTripsACheckpointRootedChain()
	{
		var chain = TestChain.CreateRootedAt(100);
		TestChain.Extend(chain, 3);

		var bytes = BlockHeaderChainFile.Serialize(chain);
		var loaded = BlockHeaderChainFile.Deserialize(bytes, Network);

		Assert.Equal(13 + 4 * 80, bytes.Length);
		Assert.Equal(100u, BlockHeaderChainFile.GetRootHeight(loaded));
		Assert.Equal(103, loaded.Tip.Height);
		Assert.Equal(chain.Tip.HashBlock, loaded.Tip.HashBlock);
		Assert.Null(loaded.GetBlock(99));
		Assert.Null(loaded.GetBlock(100).Previous);
		Assert.Equal(chain.GetBlock(100).HashBlock, loaded.GetBlock(100).HashBlock);
	}

	[Fact]
	public void LoadsTheLegacyNBitcoinFormat()
	{
		var chain = new ConcurrentChain(Network);
		TestChain.Extend(chain, 4);

		var loaded = BlockHeaderChainFile.Deserialize(chain.ToBytes(), Network);

		Assert.Equal(0u, BlockHeaderChainFile.GetRootHeight(loaded));
		Assert.Equal(chain.Tip.HashBlock, loaded.Tip.HashBlock);
	}

	[Fact]
	public void RejectsTruncatedAndBrokenFiles()
	{
		var chain = TestChain.CreateRootedAt(100);
		TestChain.Extend(chain, 2);
		var bytes = BlockHeaderChainFile.Serialize(chain);

		Assert.Throws<InvalidDataException>(() => BlockHeaderChainFile.Deserialize(bytes[..^1], Network));
		Assert.Throws<InvalidDataException>(() => BlockHeaderChainFile.Deserialize(bytes[..10], Network));

		var broken = bytes.ToArray();
		broken[13 + 80 + 10] ^= 0xff; // Second header no longer links to the root.
		Assert.Throws<ArgumentException>(() => BlockHeaderChainFile.Deserialize(broken, Network));
	}

	[Fact]
	public void CreatesACheckpointRootOnMainNetAndGenesisElsewhere()
	{
		var mainChain = BlockHeaderChainFile.CreateRooted(Network.Main, 970000);
		Assert.Equal(969696, mainChain.Tip.Height);
		Assert.Null(mainChain.Tip.Previous);
		Assert.Equal(969696u, BlockHeaderChainFile.GetRootHeight(mainChain));

		var regTestChain = BlockHeaderChainFile.CreateRooted(Network, 970000);
		Assert.Equal(0, regTestChain.Tip.Height);
		Assert.Equal(Network.GenesisHash, regTestChain.Tip.HashBlock);
	}
}

/// <summary>Builds regtest header chains with valid proof of work.</summary>
internal static class TestChain
{
	public static ConcurrentChain CreateRootedAt(int height)
	{
		var header = Network.RegTest.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = new uint256(0x1234ul);
		header.Bits = Network.RegTest.GetGenesis().Header.Bits;
		header.BlockTime = DateTimeOffset.FromUnixTimeSeconds(1_600_000_000);
		header.Nonce = 7;

		var chain = new ConcurrentChain();
		chain.SetTip(new ChainedBlock(header, height));
		return chain;
	}

	public static void Extend(ConcurrentChain chain, int count)
	{
		var tip = chain.Tip;
		for (var i = 0; i < count; i++)
		{
			tip = Mine(tip);
		}

		chain.SetTip(tip);
	}

	public static ChainedBlock Mine(ChainedBlock previous, Target? bits = null)
	{
		var header = Network.RegTest.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = previous.HashBlock;
		header.Bits = bits ?? previous.Header.Bits;
		header.BlockTime = previous.Header.BlockTime + TimeSpan.FromMinutes(10);

		for (uint nonce = 0; ; nonce++)
		{
			header.Nonce = nonce;
			if (header.CheckProofOfWork())
			{
				return new ChainedBlock(header, header.GetHash(), previous);
			}
		}
	}
}
