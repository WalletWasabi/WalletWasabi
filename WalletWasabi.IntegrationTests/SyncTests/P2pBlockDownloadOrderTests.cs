using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Extensions;
using WalletWasabi.IntegrationTests.Infrastructure;
using WalletWasabi.Services;
using WalletWasabi.Services.NodesManagement;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.IntegrationTests.SyncTests;

/// <summary>
/// Integration tests for the place of blocks in the order of the synchronization.
/// </summary>
public class P2pBlockDownloadOrderTests
{
	/// <summary>
	/// Blocks are not requested from a peer that still has block headers, filter headers or compact filters to serve.
	/// </summary>
	[Fact(Timeout = 300_000)]
	public async Task BlocksAreDownloadedLastAsync()
	{
		var cancellationToken = TestContext.Current.CancellationToken;

		// The node serves compact filters, like the peers that the filters are downloaded from.
		var coreNode = await TestNodeBuilder.CreateAsync();
		var seedNodes = (List<NetworkAddress>)Network.RegTest.SeedNodes;
		var seedNode = new NetworkAddress((System.Net.IPEndPoint)coreNode.P2pEndPoint);
		seedNodes.Add(seedNode);

		try
		{
			using var key = new Key();
			var blockHashes = await coreNode.RpcClient.GenerateToAddressAsync(3, key.GetAddress(ScriptPubKeyType.Segwit, Network.RegTest), cancellationToken);

			var syncStage = SyncStage.Blocks;
			var eventBus = new EventBus();
			using var p2pConnectionManager = new P2pConnectionManager(Network.RegTest, eventBus, DnsResolver.Instance, connectionTimeout: TimeSpan.FromSeconds(15), getSyncStage: () => syncStage);
			p2pConnectionManager.Start(cancellationToken);
			await using var ticker = new Timer(_ => eventBus.Publish(new Tick(DateTime.UtcNow)), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

			var blockProvider = BlockProviders.P2pBlockProvider(p2pConnectionManager.GetSingleUseNodeAsync);

			// Nothing but blocks is left to download. The first block comes once the peer had time to be joined by others.
			var block = await blockProvider(blockHashes[0], cancellationToken);
			Assert.Equal(blockHashes[0], block!.GetHash());
			Assert.True(Assert.Single(p2pConnectionManager.Nodes).SupportsCompactFilters);

			// Every peer serves block headers, and this one serves filter headers and compact filters too.
			foreach (var stage in new[] { SyncStage.BlockHeaders, SyncStage.FilterHeaders, SyncStage.CompactFilters })
			{
				syncStage = stage;
				var blockTask = blockProvider(blockHashes[1], cancellationToken);
				await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
				Assert.False(blockTask.IsCompleted, $"A block was downloaded during the {stage} stage.");

				// The request goes through as soon as the earlier components are complete.
				syncStage = SyncStage.Blocks;
				block = await blockTask;
				Assert.Equal(blockHashes[1], block!.GetHash());
			}
		}
		finally
		{
			seedNodes.Remove(seedNode);
			await coreNode.TryStopAsync();
		}
	}
}
