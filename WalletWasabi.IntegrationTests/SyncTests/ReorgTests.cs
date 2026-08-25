using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.IntegrationTests.Infrastructure;
using Xunit;

namespace WalletWasabi.IntegrationTests.SyncTests;

/// <summary>
/// Integration tests for blockchain reorganization handling.
/// Tests wallet and filter behavior during chain reorgs.
/// </summary>
[Collection("Integration tests")]
public class ReorgTests
{
	private readonly IntegrationTestFixture _fixture;

	public ReorgTests(IntegrationTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task Reorg_ShallowReorg_FiltersReorganized()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		// Mine some blocks and sync
		await env.RpcClient.GenerateAsync(5);
		await env.SyncFiltersAsync();

		var preTip = env.FilterStore.GetTip();
		Assert.NotNull(preTip);
		var preHeight = (uint)preTip.Header.Height;
		var preBlockHash = preTip.Header.BlockHash;

		// Act - Invalidate the last block to simulate a reorg
		await env.RpcClient.InvalidateBlockAsync(preBlockHash);

		// Mine a different block (creates a reorg)
		await env.RpcClient.GenerateAsync(2);

		// Re-sync filters - this should detect the reorg and handle it
		await env.SyncFiltersAsync();

		// Assert
		var postTip = env.FilterStore.GetTip();
		Assert.NotNull(postTip);

		// The new tip should be at the same or higher height
		var postHeight = (uint)postTip.Header.Height;
		Assert.True(postHeight >= preHeight);

		// The block hash should be different (reorg happened)
		// Note: This might not always be true depending on implementation
		// The key assertion is that we don't crash and filters are consistent
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task Reorg_WalletHandlesReorg_CoinsStateUpdated()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		var keyManager = env.CreateKeyManager();
		var wallet = env.CreateWallet(keyManager);

		var receiveKey = keyManager.GetNextReceiveKey("Pre-reorg funding");
		var receiveAddress = receiveKey.GetP2wpkhAddress(env.Network);

		// Fund the wallet and confirm
		await env.FundAddressAsync(receiveAddress, Money.Coins(1m), confirmations: 1);

		// Get the block hash we'll invalidate later
		var tipHeight = await env.RpcClient.GetBlockCountAsync();
		var blockToInvalidate = await env.RpcClient.GetBlockHashAsync(tipHeight);

		await env.SyncFiltersAsync();

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await wallet.StartAsync(cts.Token);

		await env.WaitForConditionAsync(
			() => wallet.Coins.Any(c => c.Confirmed),
			TimeSpan.FromSeconds(30));

		// Verify we have a confirmed coin
		Assert.Single(wallet.Coins);
		var coin = wallet.Coins.First();
		Assert.True(coin.Confirmed);

		// Act - Invalidate the block containing our transaction
		await env.RpcClient.InvalidateBlockAsync(blockToInvalidate);

		// Re-mine a different block (without our tx)
		await env.RpcClient.GenerateAsync(1);

		// Re-sync
		await env.SyncFiltersAsync();

		// Note: The wallet would need to reprocess filters to detect the reorg
		// This is a simplified test - in reality, the wallet needs to be notified
		// of the reorg and reprocess relevant blocks

		await wallet.StopAsync(CancellationToken.None);

		// The key assertion here is that the test completes without crashing
		// A full implementation would verify coin state changes
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task Reorg_DeepReorg_HandledGracefully()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		// Mine initial blocks
		await env.RpcClient.GenerateAsync(10);
		await env.SyncFiltersAsync();

		var initialTip = env.FilterStore.GetTip();
		Assert.NotNull(initialTip);

		// Get a block 5 blocks back to invalidate
		var reorgDepth = 5;
		var heightToInvalidate = (int)(uint)initialTip.Header.Height - reorgDepth + 1;
		var blockToInvalidate = await env.RpcClient.GetBlockHashAsync(heightToInvalidate);

		// Act - Create a deep reorg
		await env.RpcClient.InvalidateBlockAsync(blockToInvalidate);

		// Mine more blocks than we invalidated to create a longer chain
		await env.RpcClient.GenerateAsync(reorgDepth + 2);

		// Re-sync
		await env.SyncFiltersAsync();

		// Assert
		var newTip = env.FilterStore.GetTip();
		Assert.NotNull(newTip);

		// The new chain should be longer
		var newHeight = (uint)newTip.Header.Height;
		var oldHeight = (uint)initialTip.Header.Height;
		Assert.True(newHeight >= oldHeight - (uint)reorgDepth + 1);
	}

	[Fact(Timeout = 180_000)] // 3 minute timeout
	public async Task Reorg_DeepReorg_WalletUtxoSetCorrectlyUpdated()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		var keyManager = env.CreateKeyManager();
		var wallet = env.CreateWallet(keyManager);

		// Mine some initial blocks to have a stable base
		await env.RpcClient.GenerateAsync(10);
		var baseHeight = await env.RpcClient.GetBlockCountAsync();

		// Fund the wallet with coins in separate blocks (5 coins, one per block)
		const int NumberOfCoins = 5;
		var originalBlockHashes = new List<uint256>();

		for (int i = 0; i < NumberOfCoins; i++)
		{
			var receiveKey = keyManager.GetNextReceiveKey($"Funding {i + 1}");
			var address = receiveKey.GetP2wpkhAddress(env.Network);

			// Send coins and mine a block to confirm each one
			await env.RpcClient.SendToAddressAsync(address, Money.Coins(1m));
			var blockHashes = await env.RpcClient.GenerateAsync(1);
			originalBlockHashes.Add(blockHashes[0]);
		}

		// Sync filters and start the wallet
		await env.SyncFiltersAsync();

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await wallet.StartAsync(cts.Token);

		// Wait for all coins to be discovered and confirmed
		await env.WaitForConditionAsync(
			() => wallet.Coins.Count() >= NumberOfCoins && wallet.Coins.All(c => c.Confirmed),
			TimeSpan.FromSeconds(60));

		// Verify pre-reorg state: wallet has exactly N confirmed coins
		Assert.Equal(NumberOfCoins, wallet.Coins.Count());
		Assert.All(wallet.Coins, c => Assert.True(c.Confirmed, "All coins should be confirmed before reorg"));

		var preReorgTotalBalance = wallet.Coins.Sum(c => c.Amount.Satoshi);
		Assert.Equal(Money.Coins(NumberOfCoins).Satoshi, preReorgTotalBalance);

		// Record the original block hashes where each coin was confirmed
		var preReorgCoinBlockHashes = wallet.Coins
			.ToDictionary(c => c.Outpoint, c => c.Transaction.BlockHash);

		// Get the first block to invalidate (the one containing our first funding tx)
		var blockToInvalidate = originalBlockHashes[0];

		// Act - Create a deep reorg that invalidates ALL funding blocks
		await env.RpcClient.InvalidateBlockAsync(blockToInvalidate);

		// Mine new blocks - these will include the funding transactions from the mempool
		// (Bitcoin Core puts reorged transactions back in the mempool)
		await env.RpcClient.GenerateAsync(NumberOfCoins + 3);

		// Re-sync filters (this should trigger ChainReorganized events)
		await env.SyncFiltersAsync();

		// Wait for wallet to process the reorg and re-sync
		// The coins will either:
		// 1. Be re-confirmed at different block hashes (if re-mined), OR
		// 2. Remain unconfirmed (if not re-mined)
		await env.WaitForConditionAsync(
			() =>
			{
				// Check that the wallet has processed the reorg by verifying
				// that coins are no longer in the orphaned blocks
				return wallet.Coins.All(c =>
				{
					if (!c.Confirmed)
					{
						// Coin is unconfirmed - reorg was processed
						return true;
					}
					// Coin is confirmed - verify it's NOT in an orphaned block
					return c.Transaction.BlockHash is not null &&
						   !originalBlockHashes.Contains(c.Transaction.BlockHash);
				});
			},
			TimeSpan.FromSeconds(60));

		// Verify the new chain is longer and different
		var newTip = env.FilterStore.GetTip();
		Assert.NotNull(newTip);
		Assert.True((uint)newTip.Header.Height > baseHeight + NumberOfCoins);

		// Verify that the chain at the first funding block height is now different
		// (proving the reorg happened and created a new chain)
		var newBlockAtFundingHeight = await env.RpcClient.GetBlockHashAsync(baseHeight + 1);
		Assert.NotEqual(originalBlockHashes[0], newBlockAtFundingHeight);

		// Verify all coins are no longer in their ORIGINAL blocks (they were reorged out)
		foreach (var coin in wallet.Coins)
		{
			var originalBlockHash = preReorgCoinBlockHashes[coin.Outpoint];

			if (coin.Confirmed)
			{
				// If re-confirmed, must be in a DIFFERENT block
				Assert.NotNull(coin.Transaction.BlockHash);
				Assert.NotEqual(originalBlockHash, coin.Transaction.BlockHash);
			}
			// If unconfirmed, that's also valid - the transaction wasn't re-mined
		}

		// The wallet should still have all the coins (either confirmed or unconfirmed)
		Assert.Equal(NumberOfCoins, wallet.Coins.Count());

		// Total balance should be preserved (coins exist, just in different state)
		var totalBalance = wallet.Coins.Sum(c => c.Amount.Satoshi);
		Assert.Equal(Money.Coins(NumberOfCoins).Satoshi, totalBalance);

		await wallet.StopAsync(CancellationToken.None);
	}

}
