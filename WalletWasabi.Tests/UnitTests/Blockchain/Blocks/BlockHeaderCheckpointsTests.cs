using NBitcoin;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Blocks;

public class BlockHeaderCheckpointsTests
{
	public static TheoryData<Network> Networks => [Network.Main, Network.TestNet, Network.RegTest, Bitcoin.Instance.Signet];

	[Theory]
	[MemberData(nameof(Networks))]
	public void EveryFilterCheckpointHasABaseInItsDifficultyPeriod(Network network)
	{
		foreach (var checkpoint in FilterCheckpoints.GetCheckpointsByNetwork(network))
		{
			var checkpointHeight = checkpoint.Header.Height.Height;
			var (height, header) = BlockHeaderCheckpoints.GetBase(network, checkpointHeight);

			Assert.Equal(0u, height % 2016);
			Assert.InRange(height, checkpointHeight - checkpointHeight % 2016, checkpointHeight);
			Assert.True(header.CheckProofOfWork());
			if (height == checkpointHeight)
			{
				Assert.Equal(checkpoint.Header.BlockHash, header.GetHash());
			}
		}
	}
}
