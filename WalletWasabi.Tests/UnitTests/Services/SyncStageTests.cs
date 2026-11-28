using WalletWasabi.BitcoinP2p;
using WalletWasabi.Services.NodesManagement;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

/// <summary>
/// Tests for the order in which the components are downloaded from the peers, see <see cref="SyncStage"/>.
/// </summary>
public class SyncStageTests
{
	[Fact]
	public void StagesAreDeclaredInDownloadOrder()
	{
		Assert.Equal(
			[SyncStage.BlockHeaders, SyncStage.FilterHeaders, SyncStage.CompactFilters, SyncStage.Blocks],
			Enum.GetValues<SyncStage>());
	}

	/// <summary>
	/// A peer is asked for blocks when it has nothing to serve for the components that come before them:
	/// every peer serves block headers, and only some serve filter headers and compact filters.
	/// </summary>
	[Theory]
	[InlineData(SyncStage.BlockHeaders, true, false)]
	[InlineData(SyncStage.BlockHeaders, false, false)]
	[InlineData(SyncStage.FilterHeaders, true, false)]
	[InlineData(SyncStage.FilterHeaders, false, true)]
	[InlineData(SyncStage.CompactFilters, true, false)]
	[InlineData(SyncStage.CompactFilters, false, true)]
	[InlineData(SyncStage.Blocks, true, true)]
	[InlineData(SyncStage.Blocks, false, true)]
	public void BlocksAreRequestedFromPeersWithNothingElseToServe(SyncStage stage, bool peerServesCompactFilters, bool expected)
	{
		Assert.Equal(expected, stage.AllowsBlockDownloadFrom(peerServesCompactFilters));
	}

	/// <summary>
	/// Connecting to peers comes before the blocks, so that the blocks of a wallet are not all requested from
	/// the first few peers. The wait is bounded because the target may not be reachable.
	/// </summary>
	[Fact]
	public void BlocksWaitForPeers()
	{
		var target = P2pConnectionManager.TargetConnections;
		var gatheringPeriod = P2pConnectionManager.PeerGatheringPeriod;
		var almostGatheringPeriod = gatheringPeriod - TimeSpan.FromSeconds(1);

		Assert.False(P2pConnectionManager.HasEnoughPeersForBlocks(peerCount: 0, oldestConnectionAge: gatheringPeriod));
		Assert.False(P2pConnectionManager.HasEnoughPeersForBlocks(peerCount: 1, oldestConnectionAge: TimeSpan.Zero));
		Assert.False(P2pConnectionManager.HasEnoughPeersForBlocks(peerCount: target - 1, oldestConnectionAge: almostGatheringPeriod));

		Assert.True(P2pConnectionManager.HasEnoughPeersForBlocks(peerCount: target, oldestConnectionAge: TimeSpan.Zero));
		Assert.True(P2pConnectionManager.HasEnoughPeersForBlocks(peerCount: 1, oldestConnectionAge: gatheringPeriod));
	}
}
