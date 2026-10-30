using NBitcoin;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Blocks;

public class BlockHeaderCheckpointsTests
{
	[Fact]
	public void EveryMainNetFilterCheckpointHasABaseInItsDifficultyPeriod()
	{
		foreach (var checkpoint in FilterCheckpoints.GetCheckpointsByNetwork(Network.Main))
		{
			var checkpointHeight = checkpoint.Header.Height.Height;
			var (height, header) = BlockHeaderCheckpoints.GetBase(Network.Main, checkpointHeight);

			Assert.Equal(0u, height % 2016);
			Assert.InRange(height, checkpointHeight - 2015, checkpointHeight);
			Assert.True(header.CheckProofOfWork());
			if (height == checkpointHeight)
			{
				Assert.Equal(checkpoint.Header.BlockHash, header.GetHash());
			}
		}
	}

	[Fact]
	public void OtherNetworksStartAtGenesis()
	{
		var (height, header) = BlockHeaderCheckpoints.GetBase(Network.RegTest, 1000);

		Assert.Equal(0u, height);
		Assert.Equal(Network.RegTest.GenesisHash, header.GetHash());
	}
}
