using NBitcoin;
using System.Linq;
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
	private static readonly byte[] DummyFilterData = Convert.FromHexString("02832810ec08a0");

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

	/// <summary>
	/// Block headers, filter headers and compact filters are downloaded one after the other.
	/// Blocks come once all of them are complete.
	/// </summary>
	[Fact]
	public void ComponentsAreDownloadedInOrder()
	{
		var blockHeaderChain = CreateBlockHeaderChain(height: 1000);
		var filterHeaderChain = CreateFilterHeaderChain(blockHeaderChain, height: 0);
		var blockHeaderSynchronizationState = new BlockHeaderSynchronizationState();
		var state = new FilterSynchronizationState(blockHeaderChain, filterHeaderChain, tipHeight: 0, blockHeaderSynchronizationState: blockHeaderSynchronizationState);

		// The peers may have more block headers. Nothing else is requested until they are downloaded.
		Assert.Equal(SyncStage.BlockHeaders, state.CurrentStage);
		Assert.False(state.TryAssignHeaderRange(out _));
		Assert.False(state.TryAssignFilterRange(out _, out _, out _));

		// The block headers are complete. Filter headers are next, the filters wait for them.
		ReportNoMoreBlockHeaders(blockHeaderSynchronizationState, blockHeaderChain);
		Assert.Equal(SyncStage.FilterHeaders, state.CurrentStage);
		Assert.False(state.TryAssignFilterRange(out _, out _, out _));
		Assert.True(state.TryAssignHeaderRange(out var headerRange));
		Assert.Equal((1u, 1000u), (headerRange.StartHeight, headerRange.StopHeight));
		Assert.False(state.TryAssignFilterRange(out _, out _, out _));

		// The filter headers are complete. Filters are next.
		DeliverFilterHeaders(state, headerRange);
		Assert.Equal(SyncStage.CompactFilters, state.CurrentStage);
		Assert.True(state.TryAssignFilterRange(out var firstFilterRange, out _, out _));
		Assert.True(state.TryAssignFilterRange(out var secondFilterRange, out _, out _));
		Assert.Equal((1u, 500u), (firstFilterRange.StartHeight, firstFilterRange.StopHeight));
		Assert.Equal((501u, 1000u), (secondFilterRange.StartHeight, secondFilterRange.StopHeight));

		// The second half of the filters arrives first. The filters are complete when all of them are there.
		DeliverFilters(state, filterHeaderChain, secondFilterRange);
		Assert.Equal(SyncStage.CompactFilters, state.CurrentStage);
		DeliverFilters(state, filterHeaderChain, firstFilterRange);
		Assert.Equal(SyncStage.Blocks, state.CurrentStage);
	}

	/// <summary>
	/// A new block goes through the same order: its filter header, then its filter.
	/// </summary>
	[Fact]
	public void NewBlockIsDownloadedInOrder()
	{
		var blockHeaderChain = CreateBlockHeaderChain(height: 1000);
		var filterHeaderChain = CreateFilterHeaderChain(blockHeaderChain, height: 1000);
		var blockHeaderSynchronizationState = new BlockHeaderSynchronizationState();
		var state = new FilterSynchronizationState(blockHeaderChain, filterHeaderChain, tipHeight: 1000, blockHeaderSynchronizationState: blockHeaderSynchronizationState);

		ReportNoMoreBlockHeaders(blockHeaderSynchronizationState, blockHeaderChain);
		Assert.Equal(SyncStage.Blocks, state.CurrentStage);
		Assert.False(state.TryAssignHeaderRange(out _));
		Assert.False(state.TryAssignFilterRange(out _, out _, out _));

		// A node announces a new block.
		AppendBlockHeader(blockHeaderChain);
		ReportNoMoreBlockHeaders(blockHeaderSynchronizationState, blockHeaderChain);

		Assert.Equal(SyncStage.FilterHeaders, state.CurrentStage);
		Assert.False(state.TryAssignFilterRange(out _, out _, out _));
		Assert.True(state.TryAssignHeaderRange(out var headerRange));
		Assert.Equal((1001u, 1001u), (headerRange.StartHeight, headerRange.StopHeight));

		DeliverFilterHeaders(state, headerRange);
		Assert.Equal(SyncStage.CompactFilters, state.CurrentStage);
		Assert.True(state.TryAssignFilterRange(out var filterRange, out _, out _));
		Assert.Equal((1001u, 1001u), (filterRange.StartHeight, filterRange.StopHeight));

		DeliverFilters(state, filterHeaderChain, filterRange);
		Assert.Equal(SyncStage.Blocks, state.CurrentStage);
	}

	/// <summary>
	/// Without a <see cref="BlockHeaderSynchronizationState"/>, the block headers that are known are taken as complete.
	/// </summary>
	[Fact]
	public void BlockHeadersAreNotWaitedForWhenTheirStateIsNotTracked()
	{
		var state = CreateState(blockHeaders: 1000, filterHeaders: 0);

		Assert.Equal(SyncStage.FilterHeaders, state.CurrentStage);
		Assert.True(state.TryAssignHeaderRange(out _));
	}

	private static FilterSynchronizationState CreateState(int blockHeaders, int filterHeaders)
	{
		var blockHeaderChain = CreateBlockHeaderChain(blockHeaders);
		var filterHeaderChain = CreateFilterHeaderChain(blockHeaderChain, filterHeaders);

		return new FilterSynchronizationState(blockHeaderChain, filterHeaderChain, tipHeight: 0);
	}

	private static ConcurrentChain CreateBlockHeaderChain(int height)
	{
		var blockHeaderChain = new ConcurrentChain(Network);
		for (var i = 0; i < height; i++)
		{
			AppendBlockHeader(blockHeaderChain);
		}

		return blockHeaderChain;
	}

	private static void AppendBlockHeader(ConcurrentChain blockHeaderChain)
	{
		var tip = blockHeaderChain.Tip;
		var header = Network.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = tip.HashBlock;
		header.Nonce = (uint)(tip.Height + 1);
		blockHeaderChain.SetTip(new ChainedBlock(header, header.GetHash(), tip));
	}

	private static FilterHeaderChain CreateFilterHeaderChain(ConcurrentChain blockHeaderChain, int height)
	{
		var filterHeaderChain = new FilterHeaderChain();
		filterHeaderChain.AppendTip(FilterCheckpoints.GetWasabiGenesisFilter(Network).Header);
		for (var h = 1u; h <= height; h++)
		{
			filterHeaderChain.AppendTip(new SmartHeader(blockHeaderChain.GetBlock((int)h).HashBlock, new uint256(h), h, 0L));
		}

		return filterHeaderChain;
	}

	/// <summary>A node that is at the tip of the chain answers that it has no more block headers.</summary>
	private static void ReportNoMoreBlockHeaders(BlockHeaderSynchronizationState blockHeaderSynchronizationState, ConcurrentChain blockHeaderChain)
	{
		var tip = blockHeaderChain.Tip;
		blockHeaderSynchronizationState.OnHeadersProcessed(isFullMessage: false, hasExtendedChain: false, chainTip: tip, nodeTip: tip);
		Assert.True(blockHeaderSynchronizationState.IsSynchronized);
	}

	/// <summary>A node answers the request for the filter headers of the range.</summary>
	private static void DeliverFilterHeaders(FilterSynchronizationState state, RangeRequest range)
	{
		var filterHashes = Enumerable.Range((int)range.StartHeight, (int)range.Count).Select(h => new uint256((ulong)h)).ToArray();
		Assert.True(state.TryGetPreviousFilterHeader(range.StartHeight, Network, out var previousFilterHeader));

		var result = state.ValidateFilterHeaders(range, filterHashes, previousFilterHeader, Network);

		Assert.IsType<FilterSynchronizationState.HeaderValidationResult.Success>(result);
	}

	/// <summary>A node answers the request for the filters of the range.</summary>
	private static void DeliverFilters(FilterSynchronizationState state, FilterHeaderChain filterHeaderChain, RangeRequest range)
	{
		var filters = Enumerable.Range((int)range.StartHeight, (int)range.Count)
			.Select(h => new FilterModel(filterHeaderChain[(uint)h]!, new GolombRiceFilter(DummyFilterData, 20, 1 << 20)))
			.ToArray();

		state.OnFilterRangeCompleted(range.StartHeight, filters);
	}
}
