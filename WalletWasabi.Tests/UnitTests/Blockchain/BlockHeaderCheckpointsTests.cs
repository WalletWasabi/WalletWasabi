using System;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain;

public class BlockHeaderCheckpointsTests
{
	[Fact]
	public void MainNetCheckpointsAreRetargetBoundariesWithValidProofOfWork()
	{
		var interval = Network.Main.Consensus.DifficultyAdjustmentInterval;
		var checkpoints = BlockHeaderCheckpoints.GetCheckpoints(Network.Main).ToArray();

		Assert.NotEmpty(checkpoints);
		Assert.Equal(checkpoints.Select(c => c.Height).OrderBy(h => h), checkpoints.Select(c => c.Height));

		foreach (var (height, headerHex) in checkpoints)
		{
			Assert.Equal(0, height % interval);
			Assert.True(BlockHeader.Parse(headerHex, Network.Main).CheckProofOfWork(), $"Invalid proof of work at height {height}.");
		}
	}

	[Fact]
	public void SegwitActivationCheckpointMatchesTheFilterCheckpoint()
	{
		// Height 481824 is both a retarget boundary and the first filter checkpoint, so the two tables must agree.
		var root = BlockHeaderCheckpoints.TryGetRoot(Network.Main, 481824);

		Assert.NotNull(root);
		Assert.Equal(481824, root.Height);
		Assert.Null(root.Previous);
		Assert.Equal(FilterCheckpoints.GetWasabiGenesisFilter(Network.Main).Header.BlockHash, root.HashBlock);
	}

	[Fact]
	public void RootIsTheHighestCheckpointNotAboveTheRequestedHeight()
	{
		Assert.Null(BlockHeaderCheckpoints.TryGetRoot(Network.Main, 481823));
		Assert.Equal(969696, BlockHeaderCheckpoints.TryGetRoot(Network.Main, 970000)!.Height);
		Assert.Equal(969696, BlockHeaderCheckpoints.TryGetRoot(Network.Main, 1_000_000)!.Height);
		Assert.Equal(959616, BlockHeaderCheckpoints.TryGetRoot(Network.Main, 969695)!.Height);
	}

	[Fact]
	public void EveryMainNetFilterCheckpointHasAHeaderCheckpointAtOrBelowIt()
	{
		foreach (var filterCheckpoint in FilterCheckpoints.GetCheckpointsByNetwork(Network.Main))
		{
			var root = BlockHeaderCheckpoints.TryGetRoot(Network.Main, filterCheckpoint.Header.Height);

			Assert.NotNull(root);
			Assert.InRange(root.Height, filterCheckpoint.Header.Height - Network.Main.Consensus.DifficultyAdjustmentInterval + 1, filterCheckpoint.Header.Height);
		}
	}

	[Fact]
	public void OtherNetworksHaveNoCheckpoints()
	{
		Assert.Null(BlockHeaderCheckpoints.TryGetRoot(Network.TestNet, 1_000_000));
		Assert.Null(BlockHeaderCheckpoints.TryGetRoot(Network.RegTest, 1_000_000));
		Assert.Empty(BlockHeaderCheckpoints.GetCheckpoints(Network.RegTest));
	}
}
