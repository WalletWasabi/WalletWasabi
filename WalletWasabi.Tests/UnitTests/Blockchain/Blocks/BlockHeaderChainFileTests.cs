using NBitcoin;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Blocks;

public class BlockHeaderChainFileTests
{
	private const uint BaseHeight = 1000;
	private static readonly Network RegTest = Network.RegTest;

	[Fact]
	public void RoundTrip()
	{
		var chain = BuildChain(headersAfterBase: 5);
		var bytes = BlockHeaderChainFile.Serialize(chain);
		Assert.Equal(4 + 6 * 80, bytes.Length);

		var loaded = new ConcurrentChain();
		Assert.True(BlockHeaderChainFile.TryLoad(bytes, RegTest, BaseHeight, BaseHeaderOf(chain), loaded));
		Assert.Equal(chain.Tip!.HashBlock, loaded.Tip!.HashBlock);
		Assert.Equal((int)BaseHeight + 5, loaded.Tip.Height);
		Assert.Null(loaded.GetBlock((int)BaseHeight - 1));
	}

	[Fact]
	public void RejectsAFileThatDoesNotStartAtTheExpectedCheckpoint()
	{
		var chain = BuildChain(headersAfterBase: 2);
		var bytes = BlockHeaderChainFile.Serialize(chain);
		var loaded = new ConcurrentChain();

		Assert.False(BlockHeaderChainFile.TryLoad(bytes, RegTest, BaseHeight + 2016, BaseHeaderOf(chain), loaded));
		Assert.False(BlockHeaderChainFile.TryLoad(bytes, RegTest, BaseHeight, CreateHeader(uint256.Zero, nonce: 99), loaded));
		Assert.False(BlockHeaderChainFile.TryLoad(bytes[..^1], RegTest, BaseHeight, BaseHeaderOf(chain), loaded));
		Assert.Null(loaded.Tip);
	}

	[Fact]
	public void KeepsTheHeadersThatStillLink()
	{
		var chain = BuildChain(headersAfterBase: 4);
		var bytes = BlockHeaderChainFile.Serialize(chain);
		bytes[4 + 3 * 80 + 4] ^= 0xFF; // Break the previous-block link of the third header after the base.

		var loaded = new ConcurrentChain();
		Assert.True(BlockHeaderChainFile.TryLoad(bytes, RegTest, BaseHeight, BaseHeaderOf(chain), loaded));
		Assert.Equal((int)BaseHeight + 2, loaded.Tip!.Height);
		Assert.Equal(chain.GetBlock((int)BaseHeight + 2)!.HashBlock, loaded.Tip.HashBlock);
	}

	private static BlockHeader BaseHeaderOf(ConcurrentChain chain) => chain.GetBlock((int)BaseHeight)!.Header;

	private static ConcurrentChain BuildChain(int headersAfterBase)
	{
		var chain = new ConcurrentChain();
		chain.SetTip(new ChainedBlock(CreateHeader(uint256.Zero, nonce: 0), (int)BaseHeight));
		for (var i = 1; i <= headersAfterBase; i++)
		{
			var header = CreateHeader(chain.Tip!.HashBlock, nonce: (uint)i);
			chain.SetTip(new ChainedBlock(header, header.GetHash(), chain.Tip));
		}

		return chain;
	}

	private static BlockHeader CreateHeader(uint256 previous, uint nonce)
	{
		var header = RegTest.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = previous;
		header.Nonce = nonce;
		header.BlockTime = DateTimeOffset.UnixEpoch;
		return header;
	}
}
