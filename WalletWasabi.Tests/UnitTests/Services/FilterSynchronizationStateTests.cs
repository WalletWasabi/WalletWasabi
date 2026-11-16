using NBitcoin;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

/// <summary>
/// Tests for <see cref="FilterSynchronizationState"/>.
/// </summary>
public class FilterSynchronizationStateTests
{
	private static readonly Network Network = Network.RegTest;

	/// <summary>
	/// Tests compact filter assignment rules.
	/// </summary>
	[Fact]
	public void TryAssignFilterRange()
	{
		var state = CreateState(blockHeaders: 1000, filterHeaders: 1000);

		Assert.True(state.TryAssignFilterRange(out var first, out var activeCount, out var pendingCount));
		Assert.Equal(1u, first.StartHeight);
		Assert.Equal(500u, first.StopHeight);
		Assert.Equal(1, activeCount);
		Assert.Equal(0, pendingCount);

		Assert.True(state.TryAssignFilterRange(out var second, out activeCount, out pendingCount));
		Assert.Equal(501u, second.StartHeight);
		Assert.Equal(1000u, second.StopHeight);
		Assert.Equal(2, activeCount);
		Assert.Equal(0, pendingCount);

		// No more ranges available. There are only 1000 filters to fetch.
		Assert.False(state.TryAssignFilterRange(out _, out activeCount, out pendingCount));
		Assert.Equal(2, activeCount);
		Assert.Equal(0, pendingCount);

		// Node associated with the first range disconnected. The range should be available for reassignment.
		state.OnFilterNodeDisconnected(first);

		// The first range can get re-assigned.
		Assert.True(state.TryAssignFilterRange(out var reassigned, out activeCount, out pendingCount));
		Assert.Equal(first, reassigned);
		Assert.Equal(1u, reassigned.StartHeight);
		Assert.Equal(500u, reassigned.StopHeight);
		Assert.Equal(2, activeCount);
		Assert.Equal(0, pendingCount);
	}

	/// <summary>
	/// Tests filter header assignment rules.
	/// </summary>
	[Fact]
	public void TryAssignHeaderRange()
	{
		var state = CreateState(blockHeaders: 4000, filterHeaders: 0);

		Assert.True(state.TryAssignHeaderRange(out var first));
		Assert.Equal(1u, first.StartHeight);
		Assert.Equal(2000u, first.StopHeight);

		Assert.True(state.TryAssignHeaderRange(out var second));
		Assert.Equal(2001u, second.StartHeight);
		Assert.Equal(4000u, second.StopHeight);

		// No more ranges available. There are only 4000 filters to fetch.
		Assert.False(state.TryAssignHeaderRange(out _));

		// Node associated with the first header range disconnected. The range should be available for reassignment.
		state.OnHeaderNodeDisconnected(first);

		// The first range can get re-assigned.
		Assert.True(state.TryAssignHeaderRange(out var reassigned));
		Assert.Equal(first, reassigned);
		Assert.Equal(1u, reassigned.StartHeight);
		Assert.Equal(2000u, reassigned.StopHeight);
	}

	private static FilterSynchronizationState CreateState(int blockHeaders, int filterHeaders)
	{
		var blockHeaderChain = new ConcurrentChain(Network);
		var tip = blockHeaderChain.Tip;
		for (var i = 0; i < blockHeaders; i++)
		{
			var header = Network.Consensus.ConsensusFactory.CreateBlockHeader();
			header.HashPrevBlock = tip.HashBlock;
			header.Nonce = (uint)(tip.Height + 1);
			tip = new ChainedBlock(header, header.GetHash(), tip);
		}

		blockHeaderChain.SetTip(tip);

		var filterHeaderChain = new FilterHeaderChain();
		filterHeaderChain.AppendTip(FilterCheckpoints.GetWasabiGenesisFilter(Network).Header);
		for (var height = 1u; height <= filterHeaders; height++)
		{
			filterHeaderChain.AppendTip(new SmartHeader(blockHeaderChain.GetBlock((int)height).HashBlock, new uint256(height), height, 0L));
		}

		return new FilterSynchronizationState(blockHeaderChain, filterHeaderChain, tipHeight: 0);
	}
}
