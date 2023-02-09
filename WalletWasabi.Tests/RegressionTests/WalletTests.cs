using NBitcoin;
using NBitcoin.Protocol;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests;

[Collection("RegTest collection")]
public class WalletTests
{
	public WalletTests(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;
	}

	private RegTestFixture RegTestFixture { get; }

	private async Task WaitForIndexesToSyncAsync(Backend.Global global, TimeSpan timeout, BitcoinStore bitcoinStore)
	{
		var bestHash = await global.RpcClient.GetBestBlockHashAsync();

		var times = 0;
		while (bitcoinStore.SmartHeaderChain.TipHash != bestHash)
		{
			if (times > timeout.TotalSeconds)
			{
				throw new TimeoutException($"{nameof(WasabiSynchronizer)} test timed out. Filter was not downloaded.");
			}
			await Task.Delay(TimeSpan.FromSeconds(1));
			times++;
		}
	}

	[Fact]
	public async Task FilterDownloaderTestAsync()
	{
		(_, IRPCClient rpc, _, _, _, BitcoinStore bitcoinStore, _) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);
		try
		{
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(1), 1000);

			var blockCount = await rpc.GetBlockCountAsync() + 1; // Plus one because of the zeroth.
																 // Test initial synchronization.
			var times = 0;
			int filterCount;
			while ((filterCount = bitcoinStore.SmartHeaderChain.HashCount) < blockCount)
			{
				if (times > 500) // 30 sec
				{
					throw new TimeoutException($"{nameof(WasabiSynchronizer)} test timed out. Needed filters: {blockCount}, got only: {filterCount}.");
				}
				await Task.Delay(100);
				times++;
			}

			Assert.Equal(blockCount, bitcoinStore.SmartHeaderChain.HashCount);

			// Test later synchronization.
			await RegTestFixture.BackendRegTestNode.GenerateAsync(10);
			times = 0;
			while ((filterCount = bitcoinStore.SmartHeaderChain.HashCount) < blockCount + 10)
			{
				if (times > 500) // 30 sec
				{
					throw new TimeoutException($"{nameof(WasabiSynchronizer)} test timed out. Needed filters: {blockCount + 10}, got only: {filterCount}.");
				}
				await Task.Delay(100);
				times++;
			}

			// Test correct number of filters is received.
			Assert.Equal(blockCount + 10, bitcoinStore.SmartHeaderChain.HashCount);

			// Test filter block hashes are correct.
			var filterList = new List<FilterModel>();
			await bitcoinStore.IndexStore.ForeachFiltersAsync(async x =>
			{
				filterList.Add(x);
				await Task.CompletedTask;
			},
			new Height(0));
			FilterModel[] filters = filterList.ToArray();
			for (int i = 0; i < 101; i++)
			{
				var expectedHash = await rpc.GetBlockHashAsync(i);
				var filter = filters[i];
				Assert.Equal(i, (int)filter.Header.Height);
				Assert.Equal(expectedHash, filter.Header.BlockHash);
				Assert.Equal(IndexBuilderService.CreateDummyEmptyFilter(expectedHash).ToString(), filter.Filter.ToString());
			}
		}
		finally
		{
			await synchronizer.StopAsync();
		}
	}

	[Fact]
	public async Task ReorgTestAsync()
	{
		(string password, IRPCClient rpc, Network network, _, _, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		var keyManager = KeyManager.CreateNew(out _, password, network);

		// Mine some coins, make a few bech32 transactions then make it confirm.
		await rpc.GenerateAsync(1);
		var key = keyManager.GenerateNewKey(SmartLabel.Empty, KeyState.Clean, isInternal: false);
		var tx2 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));
		key = keyManager.GenerateNewKey(SmartLabel.Empty, KeyState.Clean, isInternal: false);
		var tx3 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));
		var tx4 = await rpc.SendToAddressAsync(key.GetP2pkhAddress(network), Money.Coins(0.1m));
		var tx5 = await rpc.SendToAddressAsync(key.GetP2shOverP2wpkhAddress(network), Money.Coins(0.1m));
		var tx1 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m), replaceable: true);

		await rpc.GenerateAsync(2); // Generate two, so we can test for two reorg

		var node = RegTestFixture.BackendRegTestNode;

		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);

		try
		{
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 1000);

			var reorgAwaiter = new EventsAwaiter<FilterModel>(
				h => bitcoinStore.IndexStore.Reorged += h,
				h => bitcoinStore.IndexStore.Reorged -= h,
				2);

			// Test initial synchronization.
			await WaitForIndexesToSyncAsync(global, TimeSpan.FromSeconds(90), bitcoinStore);

			var tip = await rpc.GetBestBlockHashAsync();
			Assert.Equal(tip, bitcoinStore.SmartHeaderChain.TipHash);
			var tipBlock = await rpc.GetBlockHeaderAsync(tip);
			Assert.Equal(tipBlock.HashPrevBlock, bitcoinStore.SmartHeaderChain.GetChain().Select(x => x.header.BlockHash).ToArray()[bitcoinStore.SmartHeaderChain.HashCount - 2]);

			// Test synchronization after fork.
			await rpc.InvalidateBlockAsync(tip); // Reorg 1
			tip = await rpc.GetBestBlockHashAsync();
			await rpc.InvalidateBlockAsync(tip); // Reorg 2
			var tx1bumpRes = await rpc.BumpFeeAsync(tx1); // RBF it

			await rpc.GenerateAsync(5);
			await WaitForIndexesToSyncAsync(global, TimeSpan.FromSeconds(90), bitcoinStore);

			var hashes = bitcoinStore.SmartHeaderChain.GetChain().Select(x => x.header.BlockHash).ToArray();
			Assert.DoesNotContain(tip, hashes);
			Assert.DoesNotContain(tipBlock.HashPrevBlock, hashes);

			tip = await rpc.GetBestBlockHashAsync();
			Assert.Equal(tip, bitcoinStore.SmartHeaderChain.TipHash);

			var filterList = new List<FilterModel>();
			await bitcoinStore.IndexStore.ForeachFiltersAsync(async x =>
			{
				filterList.Add(x);
				await Task.CompletedTask;
			},
			new Height(0));
			var filterTip = filterList.Last();
			Assert.Equal(tip, filterTip.Header.BlockHash);

			// Test filter block hashes are correct after fork.
			var blockCountIncludingGenesis = await rpc.GetBlockCountAsync() + 1;

			filterList.Clear();
			await bitcoinStore.IndexStore.ForeachFiltersAsync(async x =>
			{
				filterList.Add(x);
				await Task.CompletedTask;
			},
			new Height(0));
			FilterModel[] filters = filterList.ToArray();
			for (int i = 0; i < blockCountIncludingGenesis; i++)
			{
				var expectedHash = await rpc.GetBlockHashAsync(i);
				var filter = filters[i];
				Assert.Equal(i, (int)filter.Header.Height);
				Assert.Equal(expectedHash, filter.Header.BlockHash);
				if (i < 101) // Later other tests may fill the filter.
				{
					Assert.Equal(IndexBuilderService.CreateDummyEmptyFilter(expectedHash).ToString(), filter.Filter.ToString());
				}
			}

			// Test the serialization, too.
			tip = await rpc.GetBestBlockHashAsync();
			var blockHash = tip;
			for (var i = 0; i < hashes.Length; i++)
			{
				var block = await rpc.GetBlockHeaderAsync(blockHash);
				Assert.Equal(blockHash, hashes[hashes.Length - i - 1]);
				blockHash = block.HashPrevBlock;
			}

			// Assert reorg happened exactly as many times as we reorged.
			await reorgAwaiter.WaitAsync(TimeSpan.FromSeconds(10));
		}
		finally
		{
			await synchronizer.StopAsync();
		}
	}

	[Fact]
	public async Task WalletTestsAsync()
	{
		(string password, IRPCClient rpc, Network network, _, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		// Create the services.
		// 1. Create connection service.
		NodesGroup nodes = new(global.Config.Network, requirements: Constants.NodeRequirements);
		nodes.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

		Node node = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
		node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		// 2. Create wasabi synchronizer service.
		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);

		// 3. Create key manager service.
		var keyManager = KeyManager.CreateNew(out _, password, network);

		// 4. Create wallet service.
		var workDir = Helpers.Common.GetWorkDir();

		IRepository<uint256, Block> blockRepository = bitcoinStore.BlockRepository;

		using MemoryCache cache = new(new MemoryCacheOptions
		{
			SizeLimit = 1_000,
			ExpirationScanFrequency = TimeSpan.FromSeconds(30)
		});

		SmartBlockProvider blockProvider = new SmartBlockProvider(
			bitcoinStore.BlockRepository,
			null,
			new SpecificNodeBlockProvider(network, serviceConfiguration, httpClientFactory: httpClientFactory),
			new P2PBlockProvider(network, nodes, httpClientFactory),
			cache);

		using var wallet = Wallet.CreateAndRegisterServices(network, bitcoinStore, keyManager, synchronizer, workDir, serviceConfiguration, feeProvider, blockProvider);
		wallet.NewFilterProcessed += Common.Wallet_NewFilterProcessed;

		// Get some money, make it confirm.
		var key = keyManager.GetNextReceiveKey("foo label");
		var txId = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));
		await rpc.GenerateAsync(1);

		try
		{
			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 1000); // Start wasabi synchronizer service.
			await feeProvider.StartAsync(CancellationToken.None);

			// Wait until the filter our previous transaction is present.
			var blockCount = await rpc.GetBlockCountAsync();
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
			{
				await wallet.StartAsync(cts.Token); // Initialize wallet service.
			}
			Assert.Equal(1, await blockRepository.CountAsync(CancellationToken.None));

			Assert.Single(wallet.Coins);
			var firstCoin = wallet.Coins.Single();
			Assert.Equal(Money.Coins(0.1m), firstCoin.Amount);
			Assert.Equal(new Height((int)bitcoinStore.SmartHeaderChain.TipHeight), firstCoin.Height);
			Assert.InRange(firstCoin.Index, 0U, 1U);
			Assert.True(firstCoin.IsAvailable());
			Assert.Equal("foo label", firstCoin.HdPubKey.Label);
			Assert.Equal(key.P2wpkhScript, firstCoin.ScriptPubKey);
			Assert.Null(firstCoin.SpenderTransaction);
			Assert.Equal(txId, firstCoin.TransactionId);
			Assert.Single(keyManager.GetKeys(KeyState.Used, false));
			Assert.Equal("foo label", keyManager.GetKeys(KeyState.Used, false).Single().Label);

			// Get some money, make it confirm.
			var key2 = keyManager.GetNextReceiveKey("bar label");
			var txId2 = await rpc.SendToAddressAsync(key2.GetP2wpkhAddress(network), Money.Coins(0.01m));
			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			await rpc.GenerateAsync(1);
			var txId3 = await rpc.SendToAddressAsync(key2.GetP2wpkhAddress(network), Money.Coins(0.02m));
			await rpc.GenerateAsync(1);

			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 2);
			Assert.Equal(3, await blockRepository.CountAsync(CancellationToken.None));

			Assert.Equal(3, wallet.Coins.Count());
			firstCoin = wallet.Coins.OrderBy(x => x.Height).First();
			var secondCoin = wallet.Coins.OrderBy(x => x.Height).Take(2).Last();
			var thirdCoin = wallet.Coins.OrderBy(x => x.Height).Last();
			Assert.Equal(Money.Coins(0.01m), secondCoin.Amount);
			Assert.Equal(Money.Coins(0.02m), thirdCoin.Amount);
			Assert.Equal(new Height(bitcoinStore.SmartHeaderChain.TipHeight).Value - 2, firstCoin.Height.Value);
			Assert.Equal(new Height(bitcoinStore.SmartHeaderChain.TipHeight).Value - 1, secondCoin.Height.Value);
			Assert.Equal(new Height(bitcoinStore.SmartHeaderChain.TipHeight), thirdCoin.Height);
			Assert.True(thirdCoin.IsAvailable());
			Assert.Equal("foo label", firstCoin.HdPubKey.Label);
			Assert.Equal("bar label", secondCoin.HdPubKey.Label);
			Assert.Equal("bar label", thirdCoin.HdPubKey.Label);
			Assert.Equal(key.P2wpkhScript, firstCoin.ScriptPubKey);
			Assert.Equal(key2.P2wpkhScript, secondCoin.ScriptPubKey);
			Assert.Equal(key2.P2wpkhScript, thirdCoin.ScriptPubKey);
			Assert.Null(thirdCoin.SpenderTransaction);
			Assert.Equal(txId, firstCoin.TransactionId);
			Assert.Equal(txId2, secondCoin.TransactionId);
			Assert.Equal(txId3, thirdCoin.TransactionId);

			Assert.Equal(2, keyManager.GetKeys(KeyState.Used, false).Count());
			Assert.Empty(keyManager.GetKeys(KeyState.Used, true));
			Assert.Equal(2, keyManager.GetKeys(KeyState.Used).Count());
			Assert.Empty(keyManager.GetKeys(KeyState.Locked, false));
			Assert.Equal(14, keyManager.GetKeys(KeyState.Locked, true).Count());
			Assert.Equal(14, keyManager.GetKeys(KeyState.Locked).Count());
			Assert.Equal(21, keyManager.GetKeys(KeyState.Clean, true).Count());
			Assert.Equal(21, keyManager.GetKeys(KeyState.Clean, false).Count());
			Assert.Equal(42, keyManager.GetKeys(KeyState.Clean).Count());
			Assert.Equal(58, keyManager.GetKeys().Count());

			Assert.Single(keyManager.GetKeys(x => x.Label == "foo label" && x.KeyState == KeyState.Used && !x.IsInternal));
			Assert.Single(keyManager.GetKeys(x => x.Label == "bar label" && x.KeyState == KeyState.Used && !x.IsInternal));

			// REORG TESTS
			var txId4 = await rpc.SendToAddressAsync(key2.GetP2wpkhAddress(network), Money.Coins(0.03m), replaceable: true);
			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			await rpc.GenerateAsync(2);
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 2);

			Assert.NotEmpty(wallet.Coins.Where(x => x.TransactionId == txId4));
			var tip = await rpc.GetBestBlockHashAsync();
			await rpc.InvalidateBlockAsync(tip); // Reorg 1
			tip = await rpc.GetBestBlockHashAsync();
			await rpc.InvalidateBlockAsync(tip); // Reorg 2
			var tx4bumpRes = await rpc.BumpFeeAsync(txId4); // RBF it
			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			await rpc.GenerateAsync(3);
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 3);
			Assert.Equal(4, await blockRepository.CountAsync(CancellationToken.None));

			Assert.Equal(4, wallet.Coins.Count());
			Assert.Empty(wallet.Coins.Where(x => x.TransactionId == txId4));
			Assert.NotEmpty(wallet.Coins.Where(x => x.TransactionId == tx4bumpRes.TransactionId));
			var rbfCoin = wallet.Coins.Single(x => x.TransactionId == tx4bumpRes.TransactionId);

			Assert.Equal(Money.Coins(0.03m), rbfCoin.Amount);
			Assert.Equal(new Height(bitcoinStore.SmartHeaderChain.TipHeight).Value - 2, rbfCoin.Height.Value);
			Assert.True(rbfCoin.IsAvailable());
			Assert.Equal("bar label", rbfCoin.HdPubKey.Label);
			Assert.Equal(key2.P2wpkhScript, rbfCoin.ScriptPubKey);
			Assert.Null(rbfCoin.SpenderTransaction);
			Assert.Equal(tx4bumpRes.TransactionId, rbfCoin.TransactionId);

			Assert.Equal(2, keyManager.GetKeys(KeyState.Used, false).Count());
			Assert.Empty(keyManager.GetKeys(KeyState.Used, true));
			Assert.Equal(2, keyManager.GetKeys(KeyState.Used).Count());
			Assert.Empty(keyManager.GetKeys(KeyState.Locked, false));
			Assert.Equal(14, keyManager.GetKeys(KeyState.Locked, true).Count());
			Assert.Equal(14, keyManager.GetKeys(KeyState.Locked).Count());
			Assert.Equal(21, keyManager.GetKeys(KeyState.Clean, true).Count());
			Assert.Equal(21, keyManager.GetKeys(KeyState.Clean, false).Count());
			Assert.Equal(42, keyManager.GetKeys(KeyState.Clean).Count());
			Assert.Equal(58, keyManager.GetKeys().Count());

			Assert.Single(keyManager.GetKeys(KeyState.Used, false).Where(x => x.Label == "foo label"));
			Assert.Single(keyManager.GetKeys(KeyState.Used, false).Where(x => x.Label == "bar label"));

			// TEST MEMPOOL
			var txId5 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));
			await Task.Delay(1000); // Wait tx to arrive and get processed.
			Assert.NotEmpty(wallet.Coins.Where(x => x.TransactionId == txId5));
			var mempoolCoin = wallet.Coins.Single(x => x.TransactionId == txId5);
			Assert.Equal(Height.Mempool, mempoolCoin.Height);

			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			await rpc.GenerateAsync(1);
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);
			var res = await rpc.GetTxOutAsync(mempoolCoin.TransactionId, (int)mempoolCoin.Index, true);
			Assert.Equal(new Height(bitcoinStore.SmartHeaderChain.TipHeight), mempoolCoin.Height);
		}
		finally
		{
			wallet.NewFilterProcessed -= Common.Wallet_NewFilterProcessed;
			await wallet.StopAsync(CancellationToken.None);
			await synchronizer.StopAsync();
			await feeProvider.StopAsync(CancellationToken.None);
			nodes?.Dispose();
			node?.Disconnect();
		}
	}
}
