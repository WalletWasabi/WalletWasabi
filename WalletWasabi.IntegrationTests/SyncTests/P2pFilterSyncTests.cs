using System.Threading.Tasks;
using WalletWasabi.IntegrationTests.Infrastructure;
using Xunit;

namespace WalletWasabi.IntegrationTests.SyncTests;

/// <summary>
/// Integration tests for P2P filter synchronization functionality.
/// Tests downloading and processing compact block filters via the Bitcoin P2P network (BIP 157/158).
/// </summary>
[Collection("Integration tests")]
public class P2pFilterSyncTests
{
	private readonly IntegrationTestFixture _fixture;

	public P2pFilterSyncTests(IntegrationTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact(Timeout = 180_000)] // 3 minute timeout - P2P sync can be slower
	public async Task P2pFilterSync_InitialSync_AllFiltersDownloaded()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		// Get current blockchain height
		var tipHeight = await env.RpcClient.GetBlockCountAsync();

		// Act - Sync all filters via P2P
		await env.SyncFiltersP2pAsync();

		// Assert - Filter store should have filters up to tip
		var filterTip = env.FilterStore.GetTip();
		Assert.NotNull(filterTip);
		Assert.Equal((uint)tipHeight, (uint)filterTip.Header.Height);
	}

	[Fact(Timeout = 180_000)] // 3 minute timeout
	public async Task P2pFilterSync_NewBlockMined_FilterSyncs()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		// Initial sync via P2P
		await env.SyncFiltersP2pAsync();
		var initialTip = env.FilterStore.GetTip();
		Assert.NotNull(initialTip);

		var initialHeight = (uint)initialTip.Header.Height;

		// Act - Mine a new block
		await env.RpcClient.GenerateAsync(1);

		// Sync again via P2P
		await env.SyncFiltersP2pAsync();

		// Assert - Filter tip should advance by 1
		var newTip = env.FilterStore.GetTip();
		Assert.NotNull(newTip);
		Assert.Equal(initialHeight + 1, (uint)newTip.Header.Height);
	}

	[Fact(Timeout = 180_000)] // 3 minute timeout
	public async Task P2pFilterSync_MultipleBlocks_AllFiltersDownloaded()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		await env.SyncFiltersP2pAsync();
		var initialTip = env.FilterStore.GetTip();
		Assert.NotNull(initialTip);

		var initialHeight = (uint)initialTip.Header.Height;

		// Act - Mine multiple blocks
		const int blocksToMine = 10;
		await env.RpcClient.GenerateAsync(blocksToMine);

		await env.SyncFiltersP2pAsync();

		// Assert
		var newTip = env.FilterStore.GetTip();
		Assert.NotNull(newTip);
		Assert.Equal(initialHeight + blocksToMine, (uint)newTip.Header.Height);
	}

	[Fact(Timeout = 180_000)] // 3 minute timeout
	public async Task P2pFilterSync_FilterHeaderChain_UpdatesCorrectly()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		// Act
		await env.SyncFiltersP2pAsync();

		// Assert - FilterHeaderChain should be in sync
		var filterTip = env.FilterStore.GetTip();
		Assert.NotNull(filterTip);

		var chainTipHeight = env.FilterHeaderChain.TipHeight;
		Assert.Equal(filterTip.Header.Height, chainTipHeight);
	}

	[Fact(Timeout = 180_000)] // 3 minute timeout
	public async Task P2pFilterSync_EmptyChain_HandledCorrectly()
	{
		// This tests the initial state before any sync
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		// The filter store is initialized with checkpoint at height 0 for regtest
		var tip = env.FilterStore.GetTip();

		// For regtest, we expect to have at least the genesis filter
		Assert.NotNull(tip);
		Assert.True((uint)tip.Header.Height >= 0);

		// Now sync via P2P and verify we catch up
		await env.SyncFiltersP2pAsync();

		var syncedTip = env.FilterStore.GetTip();
		Assert.NotNull(syncedTip);

		var blockchainTip = await env.RpcClient.GetBlockCountAsync();
		Assert.Equal((uint)blockchainTip, (uint)syncedTip.Header.Height);
	}

	[Fact(Timeout = 300_000)] // 5 minute timeout for larger sync
	public async Task P2pFilterSync_LargerBlockBatch_SyncsCorrectly()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		// Mine a larger batch of blocks to test batched filter fetching
		const int blocksToMine = 50;
		await env.RpcClient.GenerateAsync(blocksToMine);

		// Act - Sync via P2P
		await env.SyncFiltersP2pAsync();

		// Assert
		var filterTip = env.FilterStore.GetTip();
		Assert.NotNull(filterTip);

		var blockchainTip = await env.RpcClient.GetBlockCountAsync();
		Assert.Equal((uint)blockchainTip, (uint)filterTip.Header.Height);
	}
}
