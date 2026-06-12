using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.IntegrationTests.Infrastructure;
using Xunit;

namespace WalletWasabi.IntegrationTests.SyncTests;

/// <summary>
/// Integration tests for filter synchronization functionality.
/// Tests downloading and processing compact block filters from Bitcoin Core.
/// </summary>
[Collection("Integration tests")]
public class FilterSyncTests
{
	private readonly IntegrationTestFixture _fixture;

	public FilterSyncTests(IntegrationTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task FilterSync_InitialSync_AllFiltersDownloaded()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		// Get current blockchain height
		var tipHeight = await env.RpcClient.GetBlockCountAsync();

		// Act - Sync all filters
		await env.SyncFiltersAsync();

		// Assert - Filter store should have filters up to tip
		var filterTip = env.FilterStore.GetTip();
		Assert.NotNull(filterTip);
		Assert.Equal((uint)tipHeight, (uint)filterTip.Header.Height);
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task FilterSync_NewBlockMined_FilterSyncs()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		// Initial sync
		await env.SyncFiltersAsync();
		var initialTip = env.FilterStore.GetTip();
		Assert.NotNull(initialTip);

		var initialHeight = (uint)initialTip.Header.Height;

		// Act - Mine a new block
		await env.RpcClient.GenerateAsync(1);

		// Sync again
		await env.SyncFiltersAsync();

		// Assert - Filter tip should advance by 1
		var newTip = env.FilterStore.GetTip();
		Assert.NotNull(newTip);
		Assert.Equal(initialHeight + 1, (uint)newTip.Header.Height);
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task FilterSync_MultipleBlocks_AllFiltersDownloaded()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		await env.SyncFiltersAsync();
		var initialTip = env.FilterStore.GetTip();
		Assert.NotNull(initialTip);

		var initialHeight = (uint)initialTip.Header.Height;

		// Act - Mine multiple blocks
		const int blocksToMine = 10;
		await env.RpcClient.GenerateAsync(blocksToMine);

		await env.SyncFiltersAsync();

		// Assert
		var newTip = env.FilterStore.GetTip();
		Assert.NotNull(newTip);
		Assert.Equal(initialHeight + blocksToMine, (uint)newTip.Header.Height);
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task FilterSync_FilterHeaderChain_UpdatesCorrectly()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		// Act
		await env.SyncFiltersAsync();

		// Assert - FilterHeaderChain should be in sync
		var filterTip = env.FilterStore.GetTip();
		Assert.NotNull(filterTip);

		var chainTipHeight = env.FilterHeaderChain.TipHeight;
		Assert.Equal(filterTip.Header.Height, chainTipHeight);
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task FilterSync_EmptyChain_HandledCorrectly()
	{
		// This tests the initial state before any sync
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		// The filter store is initialized with checkpoint at height 0 for regtest
		var tip = env.FilterStore.GetTip();

		// For regtest, we expect to have at least the genesis filter
		Assert.NotNull(tip);
		Assert.True((uint)tip.Header.Height >= 0);

		// Now sync and verify we catch up
		await env.SyncFiltersAsync();

		var syncedTip = env.FilterStore.GetTip();
		Assert.NotNull(syncedTip);

		var blockchainTip = await env.RpcClient.GetBlockCountAsync();
		Assert.Equal((uint)blockchainTip, (uint)syncedTip.Header.Height);
	}
}
