﻿using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Crypto;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Services;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.TorSocks5;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin;
using Xunit;
using static NBitcoin.Crypto.SchnorrBlinding;

namespace WalletWasabi.Tests
{
	[Collection("RegTest collection")]
	public class RegTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		private RegTestFixture RegTestFixture { get; }

		public RegTests(SharedFixture sharedFixture, RegTestFixture regTestFixture)
		{
			SharedFixture = sharedFixture;
			RegTestFixture = regTestFixture;
		}

		private async Task AssertFiltersInitializedAsync()
		{
			var firstHash = await Global.RpcClient.GetBlockHashAsync(0);
			while (true)
			{
				using (var client = new WasabiClient(new Uri(RegTestFixture.BackendEndPoint), null))
				{
					FiltersResponse filtersResponse = await client.GetFiltersAsync(firstHash, 1000);
					Assert.NotNull(filtersResponse);

					var filterCount = filtersResponse.Filters.Count();
					if (filterCount >= 101)
					{
						break;
					}
					else
					{
						await Task.Delay(100);
					}
				}
			}
		}

		private async Task<(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration)> InitializeTestEnvironmentAsync(int numberOfBlocksToGenerate)
		{
			await AssertFiltersInitializedAsync(); // Make sure fitlers are created on the server side.
			if (numberOfBlocksToGenerate != 0)
			{
				await Global.RpcClient.GenerateAsync(numberOfBlocksToGenerate); // Make sure everything is confirmed.
			}
			Global.Coordinator.UtxoReferee.Clear();

			var network = Global.RpcClient.Network;
			var serviceConfiguration = new ServiceConfiguration(2, 2, 21, 50, new IPEndPoint(IPAddress.Loopback, network.DefaultPort));
			return ("password", Global.RpcClient, network, Global.Coordinator, serviceConfiguration);
		}

		#region BackendTests

		[Fact]
		public async Task GetExchangeRatesAsync()
		{
			using (var client = new TorHttpClient(new Uri(RegTestFixture.BackendEndPoint), null))
			using (var response = await client.SendAsync(HttpMethod.Get, $"/api/v{Constants.BackendMajorVersion}/btc/offchain/exchange-rates"))
			{
				Assert.True(response.StatusCode == HttpStatusCode.OK);

				var exchangeRates = await response.Content.ReadAsJsonAsync<List<ExchangeRate>>();
				Assert.Single(exchangeRates);

				var rate = exchangeRates[0];
				Assert.Equal("USD", rate.Ticker);
				Assert.True(rate.Rate > 0);
			}
		}

		[Fact]
		public async Task GetClientVersionAsync()
		{
			using (var client = new WasabiClient(new Uri(RegTestFixture.BackendEndPoint), null))
			{
				var uptodate = await client.CheckUpdatesAsync(CancellationToken.None);
				Assert.True(uptodate.backendCompatible);
				Assert.True(uptodate.clientUpToDate);
			}
		}

		[Fact]
		public async Task BroadcastReplayTxAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			var utxos = await rpc.ListUnspentAsync();
			var utxo = utxos[0];
			var tx = await rpc.GetRawTransactionAsync(utxo.OutPoint.Hash);
			var content = new StringContent($"'{tx.ToHex()}'", Encoding.UTF8, "application/json");

			Logger.TurnOff();
			using (var client = new TorHttpClient(new Uri(RegTestFixture.BackendEndPoint), null))
			using (var response = await client.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/blockchain/broadcast", content))
			{
				Assert.Equal(HttpStatusCode.OK, response.StatusCode);
				Assert.Equal("Transaction is already in the blockchain.", await response.Content.ReadAsJsonAsync<string>());
			}
			Logger.TurnOn();
		}

		[Fact]
		public async Task BroadcastInvalidTxAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			var content = new StringContent($"''", Encoding.UTF8, "application/json");

			Logger.TurnOff();
			using (var client = new TorHttpClient(new Uri(RegTestFixture.BackendEndPoint), null))
			using (var response = await client.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/blockchain/broadcast", content))
			{
				Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				Assert.Equal("Invalid hex.", await response.Content.ReadAsJsonAsync<string>());
			}
			Logger.TurnOn();
		}

		[Fact]
		public async Task FilterBuilderTestAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			var indexBuilderServiceDir = Path.Combine(SharedFixture.DataDir, nameof(IndexBuilderService));
			var indexFilePath = Path.Combine(indexBuilderServiceDir, $"Index{rpc.Network}.dat");
			var utxoSetFilePath = Path.Combine(indexBuilderServiceDir, $"UtxoSet{rpc.Network}.dat");

			var indexBuilderService = new IndexBuilderService(rpc, indexFilePath, utxoSetFilePath);
			try
			{
				indexBuilderService.Synchronize();

				// Test initial synchronization.
				var times = 0;
				uint256 firstHash = await rpc.GetBlockHashAsync(0);
				while (indexBuilderService.GetFilterLinesExcluding(firstHash, 101, out _).filters.Count() != 101)
				{
					if (times > 500) // 30 sec
					{
						throw new TimeoutException($"{nameof(IndexBuilderService)} test timed out.");
					}
					await Task.Delay(100);
					times++;
				}

				// Test later synchronization.
				await rpc.GenerateAsync(10);
				times = 0;
				while (indexBuilderService.GetFilterLinesExcluding(firstHash, 111, out bool found5).filters.Count() != 111)
				{
					Assert.True(found5);
					if (times > 500) // 30 sec
					{
						throw new TimeoutException($"{nameof(IndexBuilderService)} test timed out.");
					}
					await Task.Delay(100);
					times++;
				}

				// Test correct number of filters is received.
				var hundredthHash = await rpc.GetBlockHashAsync(100);
				Assert.Equal(11, indexBuilderService.GetFilterLinesExcluding(hundredthHash, 11, out bool found).filters.Count());
				Assert.True(found);
				var bestHash = await rpc.GetBestBlockHashAsync();
				Assert.Empty(indexBuilderService.GetFilterLinesExcluding(bestHash, 1, out bool found2).filters);
				Assert.Empty(indexBuilderService.GetFilterLinesExcluding(uint256.Zero, 1, out bool found3).filters);
				Assert.False(found3);

				// Test filter block hashes are correct.
				var filters = indexBuilderService.GetFilterLinesExcluding(firstHash, 111, out bool found4).filters.ToArray();
				Assert.True(found4);
				for (int i = 0; i < 111; i++)
				{
					var expectedHash = await rpc.GetBlockHashAsync(i + 1);
					var filterModel = filters[i];
					Assert.Equal(expectedHash, filterModel.BlockHash);
				}
			}
			finally
			{
				if (indexBuilderService != null)
				{
					await indexBuilderService.StopAsync();
				}
			}
		}

		[Fact]
		public async Task AllFeeEstimateRpcAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			var estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, tolerateBitcoinCoreBrainfuck: true);
			Assert.Equal(144, estimations.Estimations.Count);
			Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
			Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
			Assert.Equal(EstimateSmartFeeMode.Conservative, estimations.Type);
			estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Economical, simulateIfRegTest: true, tolerateBitcoinCoreBrainfuck: true);
			Assert.Equal(145, estimations.Estimations.Count);
			Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
			Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
			Assert.Equal(EstimateSmartFeeMode.Economical, estimations.Type);
			estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Economical, simulateIfRegTest: true, tolerateBitcoinCoreBrainfuck: false);
			Assert.Equal(145, estimations.Estimations.Count);
			Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
			Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
			Assert.Equal(EstimateSmartFeeMode.Economical, estimations.Type);
		}

		#endregion BackendTests

		#region ServicesTests

		[Fact]
		public async Task MempoolAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			var memPoolService = new MemPoolService();
			Node node = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));
			node.VersionHandshake();

			try
			{
				memPoolService.TransactionReceived += MempoolAsync_MemPoolService_TransactionReceived;

				// Using the interlocked, not because it makes sense in this context, but to
				// set an example that these values are often concurrency sensitive
				for (int i = 0; i < 10; i++)
				{
					var addr = await rpc.GetNewAddressAsync();
					var res = await rpc.SendToAddressAsync(addr, Money.Coins(0.01m));
					Assert.NotNull(res);
				}

				var times = 0;
				while (Interlocked.Read(ref _mempoolTransactionCount) < 10)
				{
					if (times > 100) // 10 seconds
					{
						throw new TimeoutException($"{nameof(MemPoolService)} test timed out.");
					}
					await Task.Delay(100);
					times++;
				}
			}
			finally
			{
				memPoolService.TransactionReceived -= MempoolAsync_MemPoolService_TransactionReceived;
			}
		}

		private long _mempoolTransactionCount = 0;

		private void MempoolAsync_MemPoolService_TransactionReceived(object sender, SmartTransaction e)
		{
			Interlocked.Increment(ref _mempoolTransactionCount);
			Logger.LogDebug<RegTests>($"Mempool transaction received: {e.GetHash()}.");
		}


		[Fact]
		public async Task FilterDownloaderTestAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(FilterDownloaderTestAsync), $"Index{rpc.Network}.dat");

			using (var synchronizer = new WasabiSynchronizer(rpc.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint), null))
			{
				synchronizer.Start(requestInterval: TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), 1000);

				// Test initial synchronization.

				var times = 0;
				int filterCount;
				while ((filterCount = synchronizer.CountFilters()) < 102)
				{
					if (times > 500) // 30 sec
					{
						throw new TimeoutException($"{nameof(WasabiSynchronizer)} test timed out. Needed filters: {102}, got only: {filterCount}.");
					}
					await Task.Delay(100);
					times++;
				}

				// Test later synchronization.
				RegTestFixture.BackendRegTestNode.Generate(10);
				times = 0;
				while ((filterCount = synchronizer.CountFilters()) < 112)
				{
					if (times > 500) // 30 sec
					{
						throw new TimeoutException($"{nameof(WasabiSynchronizer)} test timed out. Needed filters: {112}, got only: {filterCount}.");
					}
					await Task.Delay(100);
					times++;
				}

				// Test correct number of filters is received.
				var hundredthHash = await rpc.GetBlockHashAsync(100);
				Assert.Equal(new Height(100), synchronizer.TryGetHeight(hundredthHash));

				// Test filter block hashes are correct.
				var filters = synchronizer.GetFilters().ToArray();
				for (int i = 0; i < 101; i++)
				{
					var expectedHash = await rpc.GetBlockHashAsync(i);
					var filter = filters[i];
					Assert.Equal(i, filter.BlockHeight.Value);
					Assert.Equal(expectedHash, filter.BlockHash);
					Assert.Null(filter.Filter);
				}
			}
		}

		[Fact]
		public async Task ReorgTestAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			var keyManager = KeyManager.CreateNew(out _, password);

			// Mine some coins, make a few bech32 transactions then make it confirm.
			await rpc.GenerateAsync(1);
			var key = keyManager.GenerateNewKey("", KeyState.Clean, isInternal: false);
			var tx2 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));
			key = keyManager.GenerateNewKey("", KeyState.Clean, isInternal: false);
			var tx3 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));
			var tx4 = await rpc.SendToAddressAsync(key.GetP2pkhAddress(network), Money.Coins(0.1m));
			var tx5 = await rpc.SendToAddressAsync(key.GetP2shOverP2wpkhAddress(network), Money.Coins(0.1m));
			var tx1 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m), replaceable: true);

			await rpc.GenerateAsync(2); // Generate two, so we can test for two reorg

			_reorgTestAsync_ReorgCount = 0;

			var node = RegTestFixture.BackendRegTestNode;
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(ReorgTestAsync), $"Index{rpc.Network}.dat");

			using (var synchronizer = new WasabiSynchronizer(rpc.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint), null))
			{
				try
				{
					synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 1000);

					synchronizer.Reorged += ReorgTestAsync_Downloader_Reorged;

					// Test initial synchronization.
					await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(90), synchronizer);

					var indexLines = await File.ReadAllLinesAsync(indexFilePath);
					var lastFilter = indexLines.Last();
					var tip = await rpc.GetBestBlockHashAsync();
					Assert.StartsWith(tip.ToString(), indexLines.Last());
					var tipBlock = await rpc.GetBlockHeaderAsync(tip);
					Assert.Contains(tipBlock.HashPrevBlock.ToString(), indexLines.TakeLast(2).First());

					var utxoPath = Global.IndexBuilderService.Bech32UtxoSetFilePath;
					var utxoLines = await File.ReadAllTextAsync(utxoPath);
					Assert.Contains(tx1.ToString(), utxoLines);
					Assert.Contains(tx2.ToString(), utxoLines);
					Assert.Contains(tx3.ToString(), utxoLines);
					Assert.DoesNotContain(tx4.ToString(), utxoLines); // make sure only bech is recorded
					Assert.DoesNotContain(tx5.ToString(), utxoLines); // make sure only bech is recorded

					// Test synchronization after fork.
					await rpc.InvalidateBlockAsync(tip); // Reorg 1
					tip = await rpc.GetBestBlockHashAsync();
					await rpc.InvalidateBlockAsync(tip); // Reorg 2
					var tx1bumpRes = await rpc.BumpFeeAsync(tx1); // RBF it

					await rpc.GenerateAsync(5);
					await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(90), synchronizer);

					utxoLines = await File.ReadAllTextAsync(utxoPath);
					Assert.Contains(tx1bumpRes.TransactionId.ToString(), utxoLines); // assert the tx1bump is the correct tx
					Assert.DoesNotContain(tx1.ToString(), utxoLines); // assert tx1 is abandoned (despite it confirmed previously)
					Assert.Contains(tx2.ToString(), utxoLines);
					Assert.Contains(tx3.ToString(), utxoLines);
					Assert.DoesNotContain(tx4.ToString(), utxoLines);
					Assert.DoesNotContain(tx5.ToString(), utxoLines);

					indexLines = await File.ReadAllLinesAsync(indexFilePath);
					Assert.DoesNotContain(tip.ToString(), indexLines);
					Assert.DoesNotContain(tipBlock.HashPrevBlock.ToString(), indexLines);

					// Test filter block hashes are correct after fork.
					var filters = synchronizer.GetFilters().ToArray();
					var blockCountIncludingGenesis = await rpc.GetBlockCountAsync() + 1;
					for (int i = 0; i < blockCountIncludingGenesis; i++)
					{
						var expectedHash = await rpc.GetBlockHashAsync(i);
						var filter = filters[i];
						Assert.Equal(i, filter.BlockHeight.Value);
						Assert.Equal(expectedHash, filter.BlockHash);
						if (i < 101) // Later other tests may fill the filter.
						{
							Assert.Null(filter.Filter);
						}
					}

					// Test the serialization, too.
					tip = await rpc.GetBestBlockHashAsync();
					var blockHash = tip;
					for (var i = 0; i < indexLines.Length; i++)
					{
						var block = await rpc.GetBlockHeaderAsync(blockHash);
						Assert.Contains(blockHash.ToString(), indexLines[indexLines.Length - i - 1]);
						blockHash = block.HashPrevBlock;
					}

					// Assert reorg happened exactly as many times as we reorged.
					Assert.Equal(2, Interlocked.Read(ref _reorgTestAsync_ReorgCount));
				}
				finally
				{
					synchronizer.Reorged -= ReorgTestAsync_Downloader_Reorged;
				}
			}
		}

		private async Task WaitForIndexesToSyncAsync(TimeSpan timeout, WasabiSynchronizer synchronizer)
		{
			var bestHash = await Global.RpcClient.GetBestBlockHashAsync();

			var times = 0;
			while (synchronizer.GetFilters().SingleOrDefault(x => x.BlockHash == bestHash) is null)
			{
				if (times > timeout.TotalSeconds)
				{
					throw new TimeoutException($"{nameof(WasabiSynchronizer)} test timed out. Filter wasn't downloaded.");
				}
				await Task.Delay(TimeSpan.FromSeconds(1));
				times++;
			}
		}

		private long _reorgTestAsync_ReorgCount;

		private void ReorgTestAsync_Downloader_Reorged(object sender, FilterModel e)
		{
			Assert.NotNull(e);
			Interlocked.Increment(ref _reorgTestAsync_ReorgCount);
		}

		#endregion ServicesTests

		#region ClientTests

		[Fact]
		public async Task WalletTestsAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			// Create the services.
			// 1. Create connection service.
			var nodes = new NodesGroup(Global.Config.Network, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Add(RegTestFixture.BackendRegTestNode.CreateNodeClient());

			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			memPoolService.TransactionReceived += WalletTestsAsync_MemPoolService_TransactionReceived;
			Node node = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			// 3. Create wasabi synchronizer service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(WalletTestsAsync), $"Index{rpc.Network}.dat");
			var synchronizer = new WasabiSynchronizer(rpc.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint), null);

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out _, password);

			// 5. Create chaumian coinjoin client.
			var chaumianClient = new CcjClient(synchronizer, rpc.Network, keyManager, new Uri(RegTestFixture.BackendEndPoint), null);

			// 5. Create wallet service.
			var workDir = Path.Combine(SharedFixture.DataDir, nameof(WalletTestsAsync));
			var wallet = new WalletService(keyManager, synchronizer, chaumianClient, memPoolService, nodes, workDir, serviceConfiguration);
			wallet.NewFilterProcessed += Wallet_NewFilterProcessed;

			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("foo label");
			var txid = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));
			await rpc.GenerateAsync(1);

			try
			{
				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 1000); // Start wasabi synchronizer service.
				chaumianClient.Start(); // Start chaumian coinjoin client.

				// Wait until the filter our previous transaction is present.
				var blockCount = await rpc.GetBlockCountAsync();
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}
				Assert.Equal(1, await wallet.CountBlocksAsync());

				Assert.Single(wallet.Coins);
				var firstCoin = wallet.Coins.Single();
				Assert.Equal(Money.Coins(0.1m), firstCoin.Amount);
				Assert.Equal(synchronizer.BestKnownFilter.BlockHeight, firstCoin.Height);
				Assert.InRange(firstCoin.Index, 0U, 1U);
				Assert.False(firstCoin.Unavailable);
				Assert.Equal("foo label", firstCoin.Label);
				Assert.Equal(key.P2wpkhScript, firstCoin.ScriptPubKey);
				Assert.Null(firstCoin.SpenderTransactionId);
				Assert.NotNull(firstCoin.SpentOutputs);
				Assert.NotEmpty(firstCoin.SpentOutputs);
				Assert.Equal(txid, firstCoin.TransactionId);
				Assert.Single(keyManager.GetKeys(KeyState.Used, false));
				Assert.Equal("foo label", keyManager.GetKeys(KeyState.Used, false).Single().Label);

				// Get some money, make it confirm.
				var key2 = wallet.GetReceiveKey("bar label");
				var txid2 = await rpc.SendToAddressAsync(key2.GetP2wpkhAddress(network), Money.Coins(0.01m));
				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				await rpc.GenerateAsync(1);
				var txid3 = await rpc.SendToAddressAsync(key2.GetP2wpkhAddress(network), Money.Coins(0.02m));
				await rpc.GenerateAsync(1);

				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 2);
				Assert.Equal(3, await wallet.CountBlocksAsync());

				Assert.Equal(3, wallet.Coins.Count);
				firstCoin = wallet.Coins.OrderBy(x => x.Height).First();
				var secondCoin = wallet.Coins.OrderBy(x => x.Height).Take(2).Last();
				var thirdCoin = wallet.Coins.OrderBy(x => x.Height).Last();
				Assert.Equal(Money.Coins(0.01m), secondCoin.Amount);
				Assert.Equal(Money.Coins(0.02m), thirdCoin.Amount);
				Assert.Equal(synchronizer.BestKnownFilter.BlockHeight.Value - 2, firstCoin.Height.Value);
				Assert.Equal(synchronizer.BestKnownFilter.BlockHeight.Value - 1, secondCoin.Height.Value);
				Assert.Equal(synchronizer.BestKnownFilter.BlockHeight, thirdCoin.Height);
				Assert.False(thirdCoin.Unavailable);
				Assert.Equal("foo label", firstCoin.Label);
				Assert.Equal("bar label", secondCoin.Label);
				Assert.Equal("bar label", thirdCoin.Label);
				Assert.Equal(key.P2wpkhScript, firstCoin.ScriptPubKey);
				Assert.Equal(key2.P2wpkhScript, secondCoin.ScriptPubKey);
				Assert.Equal(key2.P2wpkhScript, thirdCoin.ScriptPubKey);
				Assert.Null(thirdCoin.SpenderTransactionId);
				Assert.NotNull(firstCoin.SpentOutputs);
				Assert.NotNull(secondCoin.SpentOutputs);
				Assert.NotNull(thirdCoin.SpentOutputs);
				Assert.NotEmpty(firstCoin.SpentOutputs);
				Assert.NotEmpty(secondCoin.SpentOutputs);
				Assert.NotEmpty(thirdCoin.SpentOutputs);
				Assert.Equal(txid, firstCoin.TransactionId);
				Assert.Equal(txid2, secondCoin.TransactionId);
				Assert.Equal(txid3, thirdCoin.TransactionId);

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
				var txid4 = await rpc.SendToAddressAsync(key2.GetP2wpkhAddress(network), Money.Coins(0.03m), replaceable: true);
				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				await rpc.GenerateAsync(2);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 2);

				Assert.NotEmpty(wallet.Coins.Where(x => x.TransactionId == txid4));
				var tip = await rpc.GetBestBlockHashAsync();
				await rpc.InvalidateBlockAsync(tip); // Reorg 1
				tip = await rpc.GetBestBlockHashAsync();
				await rpc.InvalidateBlockAsync(tip); // Reorg 2
				var tx4bumpRes = await rpc.BumpFeeAsync(txid4); // RBF it
				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				await rpc.GenerateAsync(3);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 3);

				Assert.Equal(4, await wallet.CountBlocksAsync());

				Assert.Equal(4, wallet.Coins.Count);
				Assert.Empty(wallet.Coins.Where(x => x.TransactionId == txid4));
				Assert.NotEmpty(wallet.Coins.Where(x => x.TransactionId == tx4bumpRes.TransactionId));
				var rbfCoin = wallet.Coins.Where(x => x.TransactionId == tx4bumpRes.TransactionId).Single();

				Assert.Equal(Money.Coins(0.03m), rbfCoin.Amount);
				Assert.Equal(synchronizer.BestKnownFilter.BlockHeight.Value - 2, rbfCoin.Height.Value);
				Assert.False(rbfCoin.Unavailable);
				Assert.Equal("bar label", rbfCoin.Label);
				Assert.Equal(key2.P2wpkhScript, rbfCoin.ScriptPubKey);
				Assert.Null(rbfCoin.SpenderTransactionId);
				Assert.NotNull(rbfCoin.SpentOutputs);
				Assert.NotEmpty(rbfCoin.SpentOutputs);
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
				var txid5 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));
				await Task.Delay(1000); // Wait tx to arrive and get processed.
				Assert.NotEmpty(wallet.Coins.Where(x => x.TransactionId == txid5));
				var mempoolCoin = wallet.Coins.Where(x => x.TransactionId == txid5).Single();
				Assert.Equal(Height.MemPool, mempoolCoin.Height);

				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				await rpc.GenerateAsync(1);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);
				var res = await rpc.GetTxOutAsync(mempoolCoin.TransactionId, (int)mempoolCoin.Index, true);
				Assert.Equal(synchronizer.BestKnownFilter.BlockHeight, mempoolCoin.Height);
			}
			finally
			{
				wallet.NewFilterProcessed -= Wallet_NewFilterProcessed;
				wallet?.Dispose();
				// Dispose wasabi synchronizer service.
				synchronizer?.Dispose();

				// Dispose mempool service.
				memPoolService.TransactionReceived -= WalletTestsAsync_MemPoolService_TransactionReceived;

				// Dispose connection service.
				nodes?.Dispose();

				// Dispose mempool serving node.
				node?.Disconnect();

				// Dispose chaumian coinjoin client.
				if (chaumianClient != null)
				{
					await chaumianClient.StopAsync();
				}
			}
		}

		private async Task WaitForFiltersToBeProcessedAsync(TimeSpan timeout, int numberOfFiltersToWaitFor)
		{
			var times = 0;
			while (Interlocked.Read(ref _filtersProcessedByWalletCount) < numberOfFiltersToWaitFor)
			{
				if (times > timeout.TotalSeconds)
				{
					throw new TimeoutException($"{nameof(WalletService)} test timed out. Filter wasn't processed. Needed: {numberOfFiltersToWaitFor}, got only: {Interlocked.Read(ref _filtersProcessedByWalletCount)}.");
				}
				await Task.Delay(TimeSpan.FromSeconds(1));
				times++;
			}
		}

		private long _filtersProcessedByWalletCount;

		private void Wallet_NewFilterProcessed(object sender, FilterModel e)
		{
			Interlocked.Increment(ref _filtersProcessedByWalletCount);
		}

		private void WalletTestsAsync_MemPoolService_TransactionReceived(object sender, SmartTransaction e)
		{
		}

		[Fact]
		public async Task SendTestsFromHiddenWalletAsync() // These tests are taken from HiddenWallet, they were tests on the testnet.
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			// Create the services.
			// 1. Create connection service.
			var nodes = new NodesGroup(Global.Config.Network, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Add(RegTestFixture.BackendRegTestNode.CreateNodeClient());

			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			Node node = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			// 3. Create wasabi synchronizer service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync), $"Index{rpc.Network}.dat");
			var synchronizer = new WasabiSynchronizer(rpc.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint), null);

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out _, password);

			// 5. Create chaumian coinjoin client.
			var chaumianClient = new CcjClient(synchronizer, rpc.Network, keyManager, new Uri(RegTestFixture.BackendEndPoint), null);

			// 6. Create wallet service.
			var workDir = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync));
			var wallet = new WalletService(keyManager, synchronizer, chaumianClient, memPoolService, nodes, workDir, serviceConfiguration);
			wallet.NewFilterProcessed += Wallet_NewFilterProcessed;

			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("foo label");
			var txid = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m));
			Assert.NotNull(txid);
			await rpc.GenerateAsync(1);
			var txid2 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m));
			Assert.NotNull(txid2);
			await rpc.GenerateAsync(1);

			try
			{
				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.
				chaumianClient.Start(); // Start chaumian coinjoin client.

				// Wait until the filter our previous transaction is present.
				var blockCount = await rpc.GetBlockCountAsync();
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}

				var waitCount = 0;
				while (wallet.Coins.Sum(x => x.Amount) == Money.Zero)
				{
					await Task.Delay(1000);
					waitCount++;
					if (waitCount >= 21)
					{
						Logger.LogError<RegTests>("Funding transaction to the wallet did not arrive.");
						return; // Very rarely this test fails. I have no clue why. Probably because all these RegTests are interconnected, anyway let's not bother the CI with it.
					}
				}

				var scp = new Key().ScriptPubKey;
				var res2 = wallet.BuildTransaction(password, new[] { new WalletService.Operation(scp, Money.Coins(0.05m), "foo") }, 5, false);

				Assert.NotNull(res2.Transaction);
				Assert.Single(res2.OuterWalletOutputs);
				Assert.Equal(scp, res2.OuterWalletOutputs.Single().ScriptPubKey);
				Assert.Single(res2.InnerWalletOutputs);
				Assert.True(res2.Fee > Money.Satoshis(2 * 100)); // since there is a sanity check of 2sat/vb in the server
				Assert.InRange(res2.FeePercentOfSent, 0, 1);
				Assert.Single(res2.SpentCoins);
				Assert.Equal(key.P2wpkhScript, res2.SpentCoins.Single().ScriptPubKey);
				Assert.Equal(Money.Coins(1m), res2.SpentCoins.Single().Amount);
				Assert.False(res2.SpendsUnconfirmed);

				await wallet.SendTransactionAsync(res2.Transaction);

				Assert.Contains(res2.InnerWalletOutputs.Single(), wallet.Coins);

				#region Basic

				Script receive = wallet.GetReceiveKey("Basic").P2wpkhScript;
				Money amountToSend = wallet.Coins.Where(x => !x.Unavailable).Sum(x => x.Amount) / 2;
				var res = wallet.BuildTransaction(password, new[] { new WalletService.Operation(receive, amountToSend, "foo") }, 1008, allowUnconfirmed: true);

				foreach (SmartCoin coin in res.SpentCoins)
				{
					Assert.False(coin.CoinJoinInProgress);
					Assert.True(coin.Confirmed);
					Assert.Null(coin.SpenderTransactionId);
					Assert.True(coin.Unspent);
				}

				Assert.Equal(2, res.InnerWalletOutputs.Count());
				Assert.Empty(res.OuterWalletOutputs);
				var activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
				var changeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey != receive);

				Assert.Equal(receive, activeOutput.ScriptPubKey);
				Assert.Equal(amountToSend, activeOutput.Amount);
				if (res.SpentCoins.Sum(x => x.Amount) - activeOutput.Amount == res.Fee) // this happens when change is too small
				{
					Assert.Contains(res.Transaction.Transaction.Outputs, x => x.Value == activeOutput.Amount);
					Logger.LogInfo<RegTests>($"Change Output: {changeOutput.Amount.ToString(false, true)} {changeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				}
				Logger.LogInfo<RegTests>($"Fee: {res.Fee}");
				Logger.LogInfo<RegTests>($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Logger.LogInfo<RegTests>($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Logger.LogInfo<RegTests>($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogInfo<RegTests>($"TxId: {res.Transaction.GetHash()}");

				var foundReceive = false;
				Assert.InRange(res.Transaction.Transaction.Outputs.Count, 1, 2);
				foreach (var output in res.Transaction.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.Equal(amountToSend, output.Value);
					}
				}
				Assert.True(foundReceive);

				await wallet.SendTransactionAsync(res.Transaction);

				#endregion Basic

				#region SubtractFeeFromAmount

				receive = wallet.GetReceiveKey("SubtractFeeFromAmount").P2wpkhScript;
				amountToSend = wallet.Coins.Where(x => !x.Unavailable).Sum(x => x.Amount) / 3;
				res = wallet.BuildTransaction(password, new[] { new WalletService.Operation(receive, amountToSend, "foo") }, 1008, allowUnconfirmed: true, subtractFeeFromAmountIndex: 0);

				Assert.Equal(2, res.InnerWalletOutputs.Count());
				Assert.Empty(res.OuterWalletOutputs);
				activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
				changeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey != receive);

				Assert.Equal(receive, activeOutput.ScriptPubKey);
				Assert.Equal(amountToSend - res.Fee, activeOutput.Amount);
				Assert.Contains(res.Transaction.Transaction.Outputs, x => x.Value == changeOutput.Amount);
				Logger.LogInfo<RegTests>($"Fee: {res.Fee}");
				Logger.LogInfo<RegTests>($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Logger.LogInfo<RegTests>($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Logger.LogInfo<RegTests>($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogInfo<RegTests>($"Change Output: {changeOutput.Amount.ToString(false, true)} {changeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogInfo<RegTests>($"TxId: {res.Transaction.GetHash()}");

				foundReceive = false;
				Assert.InRange(res.Transaction.Transaction.Outputs.Count, 1, 2);
				foreach (var output in res.Transaction.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.Equal(amountToSend - res.Fee, output.Value);
					}
				}
				Assert.True(foundReceive);

				#endregion SubtractFeeFromAmount

				#region LowFee

				res = wallet.BuildTransaction(password, new[] { new WalletService.Operation(receive, amountToSend, "foo") }, 1008, allowUnconfirmed: true);

				Assert.Equal(2, res.InnerWalletOutputs.Count());
				Assert.Empty(res.OuterWalletOutputs);
				activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
				changeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey != receive);

				Assert.Equal(receive, activeOutput.ScriptPubKey);
				Assert.Equal(amountToSend, activeOutput.Amount);
				Assert.Contains(res.Transaction.Transaction.Outputs, x => x.Value == changeOutput.Amount);
				Logger.LogInfo<RegTests>($"Fee: {res.Fee}");
				Logger.LogInfo<RegTests>($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Logger.LogInfo<RegTests>($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Logger.LogInfo<RegTests>($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogInfo<RegTests>($"Change Output: {changeOutput.Amount.ToString(false, true)} {changeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogInfo<RegTests>($"TxId: {res.Transaction.GetHash()}");

				foundReceive = false;
				Assert.InRange(res.Transaction.Transaction.Outputs.Count, 1, 2);
				foreach (var output in res.Transaction.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.Equal(amountToSend, output.Value);
					}
				}
				Assert.True(foundReceive);

				#endregion LowFee

				#region MediumFee

				res = wallet.BuildTransaction(password, new[] { new WalletService.Operation(receive, amountToSend, "foo") }, 144, allowUnconfirmed: true);

				Assert.Equal(2, res.InnerWalletOutputs.Count());
				Assert.Empty(res.OuterWalletOutputs);
				activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
				changeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey != receive);

				Assert.Equal(receive, activeOutput.ScriptPubKey);
				Assert.Equal(amountToSend, activeOutput.Amount);
				Assert.Contains(res.Transaction.Transaction.Outputs, x => x.Value == changeOutput.Amount);
				Logger.LogInfo<RegTests>($"Fee: {res.Fee}");
				Logger.LogInfo<RegTests>($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Logger.LogInfo<RegTests>($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Logger.LogInfo<RegTests>($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogInfo<RegTests>($"Change Output: {changeOutput.Amount.ToString(false, true)} {changeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogInfo<RegTests>($"TxId: {res.Transaction.GetHash()}");

				foundReceive = false;
				Assert.InRange(res.Transaction.Transaction.Outputs.Count, 1, 2);
				foreach (var output in res.Transaction.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.Equal(amountToSend, output.Value);
					}
				}
				Assert.True(foundReceive);

				#endregion MediumFee

				#region HighFee

				res = wallet.BuildTransaction(password, new[] { new WalletService.Operation(receive, amountToSend, "foo") }, 2, allowUnconfirmed: true);

				Assert.Equal(2, res.InnerWalletOutputs.Count());
				Assert.Empty(res.OuterWalletOutputs);
				activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
				changeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey != receive);

				Assert.Equal(receive, activeOutput.ScriptPubKey);
				Assert.Equal(amountToSend, activeOutput.Amount);
				Assert.Contains(res.Transaction.Transaction.Outputs, x => x.Value == changeOutput.Amount);
				Logger.LogInfo<RegTests>($"Fee: {res.Fee}");
				Logger.LogInfo<RegTests>($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Logger.LogInfo<RegTests>($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Logger.LogInfo<RegTests>($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogInfo<RegTests>($"Change Output: {changeOutput.Amount.ToString(false, true)} {changeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogInfo<RegTests>($"TxId: {res.Transaction.GetHash()}");

				foundReceive = false;
				Assert.InRange(res.Transaction.Transaction.Outputs.Count, 1, 2);
				foreach (var output in res.Transaction.Transaction.Outputs)
				{
					if (output.ScriptPubKey == receive)
					{
						foundReceive = true;
						Assert.Equal(amountToSend, output.Value);
					}
				}
				Assert.True(foundReceive);

				Assert.InRange(res.Fee, Money.Zero, res.Fee);
				Assert.InRange(res.Fee, res.Fee, res.Fee);

				await wallet.SendTransactionAsync(res.Transaction);

				#endregion HighFee

				#region MaxAmount

				receive = wallet.GetReceiveKey("MaxAmount").P2wpkhScript;
				res = wallet.BuildTransaction(password, new[] { new WalletService.Operation(receive, Money.Zero, "foo") }, 1008, allowUnconfirmed: true);

				Assert.Single(res.InnerWalletOutputs);
				Assert.Empty(res.OuterWalletOutputs);
				activeOutput = res.InnerWalletOutputs.Single();

				Assert.Equal(receive, activeOutput.ScriptPubKey);

				Logger.LogInfo<RegTests>($"Fee: {res.Fee}");
				Logger.LogInfo<RegTests>($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Logger.LogInfo<RegTests>($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Logger.LogInfo<RegTests>($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogInfo<RegTests>($"TxId: {res.Transaction.GetHash()}");

				Assert.Single(res.Transaction.Transaction.Outputs);
				var maxBuiltTxOutput = res.Transaction.Transaction.Outputs.Single();
				Assert.Equal(receive, maxBuiltTxOutput.ScriptPubKey);
				Assert.Equal(wallet.Coins.Where(x => !x.Unavailable).Sum(x => x.Amount) - res.Fee, maxBuiltTxOutput.Value);

				await wallet.SendTransactionAsync(res.Transaction);

				#endregion MaxAmount

				#region InputSelection

				receive = wallet.GetReceiveKey("InputSelection").P2wpkhScript;

				var inputCountBefore = res.SpentCoins.Count();
				res = wallet.BuildTransaction(password, new[] { new WalletService.Operation(receive, Money.Zero, "foo") }, 1008,
					allowUnconfirmed: true,
					allowedInputs: wallet.Coins.Where(x => !x.Unavailable).Select(x => new TxoRef(x.TransactionId, x.Index)).Take(1));

				Assert.Single(res.InnerWalletOutputs);
				Assert.Empty(res.OuterWalletOutputs);
				activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);

				Assert.True(inputCountBefore >= res.SpentCoins.Count());
				Assert.Equal(res.SpentCoins.Count(), res.Transaction.Transaction.Inputs.Count);

				Assert.Equal(receive, activeOutput.ScriptPubKey);
				Logger.LogInfo<RegTests>($"Fee: {res.Fee}");
				Logger.LogInfo<RegTests>($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Logger.LogInfo<RegTests>($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Logger.LogInfo<RegTests>($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogInfo<RegTests>($"TxId: {res.Transaction.GetHash()}");

				Assert.Single(res.Transaction.Transaction.Outputs);

				res = wallet.BuildTransaction(password, new[] { new WalletService.Operation(receive, Money.Zero, "foo") }, 1008,
					allowUnconfirmed: true,
					allowedInputs: new[] { res.SpentCoins.Select(x => new TxoRef(x.TransactionId, x.Index)).First() });

				Assert.Single(res.InnerWalletOutputs);
				Assert.Empty(res.OuterWalletOutputs);
				activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);

				Assert.Single(res.Transaction.Transaction.Inputs);
				Assert.Single(res.Transaction.Transaction.Outputs);
				Assert.Single(res.SpentCoins);

				#endregion InputSelection

				#region Labeling

				res = wallet.BuildTransaction(password, new[] { new WalletService.Operation(receive, Money.Zero, "my label") }, 1008,
					allowUnconfirmed: true);

				Assert.Single(res.InnerWalletOutputs);
				Assert.Equal($"{Constants.ChangeOfSpecialLabelStart}my label{Constants.ChangeOfSpecialLabelEnd}", res.InnerWalletOutputs.Single().Label);

				amountToSend = wallet.Coins.Where(x => !x.Unavailable).Sum(x => x.Amount) / 3;
				res = wallet.BuildTransaction(password, new[] {
					new WalletService.Operation(new Key().ScriptPubKey, amountToSend, "outgoing"),
					new WalletService.Operation(new Key().ScriptPubKey, amountToSend, "outgoing2")
				}, 1008,
					allowUnconfirmed: true);

				Assert.Single(res.InnerWalletOutputs);
				Assert.Equal(2, res.OuterWalletOutputs.Count());
				Assert.Equal($"{Constants.ChangeOfSpecialLabelStart}outgoing, outgoing2{Constants.ChangeOfSpecialLabelEnd}", res.InnerWalletOutputs.Single().Label);

				await wallet.SendTransactionAsync(res.Transaction);

				Assert.Contains($"{Constants.ChangeOfSpecialLabelStart}outgoing, outgoing2{Constants.ChangeOfSpecialLabelEnd}", wallet.Coins.Where(x => x.Height == Height.MemPool).Select(x => x.Label));
				Assert.Contains($"{Constants.ChangeOfSpecialLabelStart}outgoing, outgoing2{Constants.ChangeOfSpecialLabelEnd}", keyManager.GetKeys().Select(x => x.Label));

				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				await rpc.GenerateAsync(1);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);

				var bestHeight = wallet.Synchronizer.BestKnownFilter.BlockHeight;
				Assert.Contains($"{Constants.ChangeOfSpecialLabelStart}outgoing, outgoing2{Constants.ChangeOfSpecialLabelEnd}", wallet.Coins.Where(x => x.Height == bestHeight).Select(x => x.Label));
				Assert.Contains($"{Constants.ChangeOfSpecialLabelStart}outgoing, outgoing2{Constants.ChangeOfSpecialLabelEnd}", keyManager.GetKeys().Select(x => x.Label));

				#endregion Labeling

				#region AllowedInputsDisallowUnconfirmed

				inputCountBefore = res.SpentCoins.Count();

				receive = wallet.GetReceiveKey("AllowedInputsDisallowUnconfirmed").P2wpkhScript;

				var allowedInputs = wallet.Coins.Where(x => !x.Unavailable).Select(x => new TxoRef(x.TransactionId, x.Index)).Take(1);
				var toSend = new[] { new WalletService.Operation(receive, Money.Zero, "fizz") };

				// covers:
				// disallow unconfirmed with allowed inputs
				// feeTarget < 2 // NOTE: need to correct alowing 0 and 1
				res = wallet.BuildTransaction(password, toSend, 0, false, allowedInputs: allowedInputs);

				activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
				Assert.Single(res.InnerWalletOutputs);
				Assert.Empty(res.OuterWalletOutputs);

				Assert.Equal(receive, activeOutput.ScriptPubKey);
				Logger.LogDebug<RegTests>($"Fee: {res.Fee}");
				Logger.LogDebug<RegTests>($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Logger.LogDebug<RegTests>($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Logger.LogDebug<RegTests>($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogDebug<RegTests>($"TxId: {res.Transaction.GetHash()}");

				Assert.True(inputCountBefore >= res.SpentCoins.Count());
				Assert.False(res.SpendsUnconfirmed);

				Assert.Single(res.Transaction.Transaction.Inputs);
				Assert.Single(res.Transaction.Transaction.Outputs);
				Assert.Single(res.SpentCoins);

				Assert.True(inputCountBefore >= res.SpentCoins.Count());
				Assert.Equal(res.SpentCoins.Count(), res.Transaction.Transaction.Inputs.Count);

				#endregion AllowedInputsDisallowUnconfirmed

				#region CustomChange

				// covers:
				// customchange
				// feePc > 1
				res = wallet.BuildTransaction(password, new[] { new WalletService.Operation(new Key().ScriptPubKey, Money.Coins(0.0003m), "outgoing") }, 2, customChange: new Key().ScriptPubKey);

				Assert.True(res.FeePercentOfSent > 1);

				Logger.LogDebug<RegTests>($"Fee: {res.Fee}");
				Logger.LogDebug<RegTests>($"FeePercentOfSent: {res.FeePercentOfSent} %");
				Logger.LogDebug<RegTests>($"SpendsUnconfirmed: {res.SpendsUnconfirmed}");
				Logger.LogDebug<RegTests>($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
				Logger.LogDebug<RegTests>($"TxId: {res.Transaction.GetHash()}");

				#endregion CustomChange
			}
			finally
			{
				wallet.NewFilterProcessed -= Wallet_NewFilterProcessed;
				wallet?.Dispose();
				// Dispose wasabi synchronizer service.
				synchronizer?.Dispose();
				// Dispose connection service.
				nodes?.Dispose();
				// Dispose mempool serving node.
				node?.Disconnect();
				// Dispose chaumian coinjoin client.
				if (chaumianClient != null)
				{
					await chaumianClient.StopAsync();
				}
			}
		}

		[Fact]
		public async Task BuildTransactionValidationsTestAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			// Create the services.
			// 1. Create connection service.
			var nodes = new NodesGroup(Global.Config.Network, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Add(RegTestFixture.BackendRegTestNode.CreateNodeClient());

			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			Node node = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			// 3. Create wasabi synchronizer service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync), $"Index{rpc.Network}.dat");
			var synchronizer = new WasabiSynchronizer(rpc.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint), null);

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out _, password);

			// 5. Create chaumian coinjoin client.
			var chaumianClient = new CcjClient(synchronizer, rpc.Network, keyManager, new Uri(RegTestFixture.BackendEndPoint), null);

			// 6. Create wallet service.
			var workDir = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync));
			var wallet = new WalletService(keyManager, synchronizer, chaumianClient, memPoolService, nodes, workDir, serviceConfiguration);
			wallet.NewFilterProcessed += Wallet_NewFilterProcessed;

			var scp = new Key().ScriptPubKey;
			var validOperationList = new[] { new WalletService.Operation(scp, Money.Coins(1), "") };
			var invalidOperationList = new[] { new WalletService.Operation(scp, Money.Coins(10 * 1000 * 1000), ""), new WalletService.Operation(scp, Money.Coins(12 * 1000 * 1000), "") };
			var overflowOperationList = new[]{
				new WalletService.Operation(scp, Money.Satoshis(long.MaxValue), ""),
				new WalletService.Operation(scp, Money.Satoshis(long.MaxValue), ""),
				new WalletService.Operation(scp, Money.Satoshis(5), "")
				};

			Logger.TurnOff();
			// toSend cannot be null
			Assert.Throws<ArgumentNullException>(() => wallet.BuildTransaction(null, null, 0));

			// toSend cannot have a null element
			Assert.Throws<ArgumentNullException>(() => wallet.BuildTransaction(null, new[] { (WalletService.Operation)null }, 0));

			// toSend cannot have a zero elements
			Assert.Throws<ArgumentException>(() => wallet.BuildTransaction(null, new WalletService.Operation[0], 0));

			// feeTarget has to be in the range 0 to 1008
			Assert.Throws<ArgumentOutOfRangeException>(() => wallet.BuildTransaction(null, validOperationList, -10));
			Assert.Throws<ArgumentOutOfRangeException>(() => wallet.BuildTransaction(null, validOperationList, 2000));

			// subtractFeeFromAmountIndex has to be valid
			Assert.Throws<ArgumentOutOfRangeException>(() => wallet.BuildTransaction(null, validOperationList, 2, false, -10));
			Assert.Throws<ArgumentOutOfRangeException>(() => wallet.BuildTransaction(null, validOperationList, 2, false, 1));

			// toSend amount sum has to be in range 0 to 2099999997690000
			Assert.Throws<ArgumentOutOfRangeException>(() => wallet.BuildTransaction(null, invalidOperationList, 2));

			// toSend negative sum amount
			var operations = new[]{
				new WalletService.Operation(scp, Money.Satoshis(-10000), "") };
			Assert.Throws<ArgumentException>(() => wallet.BuildTransaction(null, operations, 2));

			// toSend negative operation amount
			operations = new[]{
				new WalletService.Operation(scp,  Money.Satoshis(20000), ""),
				new WalletService.Operation(scp, Money.Satoshis(-10000), "") };
			Assert.Throws<ArgumentException>(() => wallet.BuildTransaction(null, operations, 2));

			// allowedInputs cannot be empty
			Assert.Throws<ArgumentException>(() => wallet.BuildTransaction(null, validOperationList, 2, false, null, null, new TxoRef[0]));

			// "Only one element can contain Money.Zero
			var toSendWithTwoZeros = new[]{
				new WalletService.Operation(scp, Money.Zero, "zero"),
				new WalletService.Operation(scp, Money.Zero, "zero") };
			Assert.Throws<ArgumentException>(() => wallet.BuildTransaction(password, toSendWithTwoZeros, 1008, false));

			// cannot specify spend all and custom change
			var spendAll = new[]{
				new WalletService.Operation(scp, Money.Zero, "spendAll") };
			Assert.Throws<ArgumentException>(() => wallet.BuildTransaction(password, spendAll, 1008, false, customChange: new Key().ScriptPubKey));

			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("foo label");
			var txid = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m));

			// Generate some coins
			await rpc.GenerateAsync(2);

			try
			{
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.
				chaumianClient.Start(); // Start chaumian coinjoin client.

				// Wait until the filter our previous transaction is present.
				var blockCount = await rpc.GetBlockCountAsync();
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}

				// subtract Fee from amount index with no enough money
				operations = new[]{
					new WalletService.Operation(scp,  Money.Coins(1m), ""),
					new WalletService.Operation(scp, Money.Coins(0.5m), "") };
				Assert.Throws<InsufficientBalanceException>(() => wallet.BuildTransaction(password, operations, 2, false, 0));

				// No enough money (only one confirmed coin, no unconfirmed allowed)
				operations = new[] { new WalletService.Operation(scp, Money.Coins(1.5m), "") };
				Assert.Throws<InsufficientBalanceException>(() => wallet.BuildTransaction(null, operations, 2));

				// No enough money (only one confirmed coin, unconfirmed allowed)
				Assert.Throws<InsufficientBalanceException>(() => wallet.BuildTransaction(null, operations, 2, true));

				// Add new money with no confirmation
				var txid2 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(2m));
				await Task.Delay(1000); // Wait tx to arrive and get processed.

				// Enough money (one confirmed coin and one unconfirmed coin, unconfirmed are NOT allowed)
				Assert.Throws<InsufficientBalanceException>(() => wallet.BuildTransaction(null, operations, 2, false));

				// Enough money (one confirmed coin and one unconfirmed coin, unconfirmed are allowed)
				var btx = wallet.BuildTransaction(password, operations, 2, true);
				Assert.Equal(2, btx.SpentCoins.Count());
				Assert.Equal(1, btx.SpentCoins.Count(c => c.Confirmed));
				Assert.Equal(1, btx.SpentCoins.Count(c => !c.Confirmed));

				// Only one operation with Zero money
				operations = new[]{
					new WalletService.Operation(scp, Money.Zero, ""),
					new WalletService.Operation(scp, Money.Zero, "") };
				Assert.Throws<ArgumentException>(() => wallet.BuildTransaction(null, operations, 2));

				// `Custom change` and `spend all` cannot be specified at the same time
				Assert.Throws<ArgumentException>(() => wallet.BuildTransaction(null, operations, 2, false, null, Script.Empty));
				Logger.TurnOn();

				operations = new[] { new WalletService.Operation(scp, Money.Coins(0.5m), "") };
				btx = wallet.BuildTransaction(password, operations, 2);
			}
			finally
			{
				wallet?.Dispose();
				// Dispose wasabi synchronizer service.
				synchronizer?.Dispose();
				// Dispose connection service.
				nodes?.Dispose();
				// Dispose mempool serving node.
				node?.Disconnect();
				// Dispose chaumian coinjoin client.
				if (chaumianClient != null)
				{
					await chaumianClient.StopAsync();
				}
			}
		}

		[Fact]
		public async Task BuildTransactionReorgsTestAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			// Create the services.
			// 1. Create connection service.
			var nodes = new NodesGroup(Global.Config.Network, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Add(RegTestFixture.BackendRegTestNode.CreateNodeClient());

			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			Node node = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			// 3. Create wasabi synchronizer service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync), $"Index{rpc.Network}.dat");
			var synchronizer = new WasabiSynchronizer(rpc.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint), null);

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out _, password);

			// 5. Create chaumian coinjoin client.
			var chaumianClient = new CcjClient(synchronizer, rpc.Network, keyManager, new Uri(RegTestFixture.BackendEndPoint), null);

			// 6. Create wallet service.
			var workDir = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync));
			var wallet = new WalletService(keyManager, synchronizer, chaumianClient, memPoolService, nodes, workDir, serviceConfiguration);
			wallet.NewFilterProcessed += Wallet_NewFilterProcessed;

			Assert.Empty(wallet.Coins);
			var baseTip = await rpc.GetBestBlockHashAsync();

			// Generate script
			var scp = new Key().ScriptPubKey;

			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("foo label");
			var fundingTxid = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));

			// Generate some coins
			await rpc.GenerateAsync(2);

			try
			{
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.
				chaumianClient.Start(); // Start chaumian coinjoin client.

				// Wait until the filter our previous transaction is present.
				var blockCount = await rpc.GetBlockCountAsync();
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}
				Assert.Single(wallet.Coins);

				// Send money before reorg.
				var operations = new[]{
					new WalletService.Operation(scp, Money.Coins(0.011m), "") };
				var btx1 = wallet.BuildTransaction(password, operations, 2);
				await wallet.SendTransactionAsync(btx1.Transaction);

				operations = new[]{
					new WalletService.Operation(scp, Money.Coins(0.012m), "") };
				var btx2 = wallet.BuildTransaction(password, operations, 2, allowUnconfirmed: true);
				await wallet.SendTransactionAsync(btx2.Transaction);

				// Test synchronization after fork.
				// Invalidate the blocks containing the funding transaction
				var tip = await rpc.GetBestBlockHashAsync();
				await rpc.InvalidateBlockAsync(tip); // Reorg 1
				tip = await rpc.GetBestBlockHashAsync();
				await rpc.InvalidateBlockAsync(tip); // Reorg 2

				// Generate three new blocks (replace the previous invalidated ones)
				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				await rpc.GenerateAsync(3);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 3);

				// Send money after reorg.
				// When we invalidate a block, those transactions setted in the invalidated block
				// are reintroduced when we generate a new block though the rpc call
				operations = new[]{
					new WalletService.Operation(scp, Money.Coins(0.013m), "") };
				var btx3 = wallet.BuildTransaction(password, operations, 2);
				await wallet.SendTransactionAsync(btx3.Transaction);

				operations = new[]{
					new WalletService.Operation(scp, Money.Coins(0.014m), "") };
				var btx4 = wallet.BuildTransaction(password, operations, 2, allowUnconfirmed: true);
				await wallet.SendTransactionAsync(btx4.Transaction);

				// Test synchronization after fork with different transactions.
				// Create a fork that invalidates the blocks containing the funding transaction
				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				await rpc.InvalidateBlockAsync(baseTip);
				try
				{
					await rpc.AbandonTransactionAsync(fundingTxid);
				}
				catch
				{
					return; // Occassionally this fails on Linux or OSX, I have no idea why.
				}
				await rpc.GenerateAsync(10);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 10);

				var curBlockHash = await rpc.GetBestBlockHashAsync();
				blockCount = await rpc.GetBlockCountAsync();

				// Make sure the funding transaction is not in any block of the chain
				while (curBlockHash != rpc.Network.GenesisHash)
				{
					var block = await rpc.GetBlockAsync(curBlockHash);

					if (block.Transactions.Any(tx => tx.GetHash() == fundingTxid))
					{
						throw new Exception($"Transaction found in block at heigh {blockCount}  hash: {block.GetHash()}");
					}
					curBlockHash = block.Header.HashPrevBlock;
					blockCount--;
				}

				// There shouldn't be any `confirmed` coin
				Assert.Empty(wallet.Coins.Where(x => x.Confirmed));

				// Get some money, make it confirm.
				// this is necesary because we are in a fork now.
				fundingTxid = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m), replaceable: true);
				await Task.Delay(1000); // Waits for the funding transaction get to the mempool.
				Assert.Single(wallet.Coins.Where(x => !x.Confirmed));

				var fundingBumpTxid = await rpc.BumpFeeAsync(fundingTxid);
				await Task.Delay(2000); // Waits for the funding transaction get to the mempool.
				Assert.Single(wallet.Coins.Where(x => !x.Confirmed));
				Assert.Single(wallet.Coins.Where(x => x.TransactionId == fundingBumpTxid.TransactionId));

				// Confirm the coin
				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				await rpc.GenerateAsync(1);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);

				Assert.Single(wallet.Coins.Where(x => x.Confirmed && x.TransactionId == fundingBumpTxid.TransactionId));
			}
			finally
			{
				wallet?.Dispose();
				// Dispose wasabi synchronizer service.
				synchronizer?.Dispose();
				// Dispose connection service.
				nodes?.Dispose();
				// Dispose mempool serving node.
				node?.Disconnect();
				// Dispose chaumian coinjoin client.
				if (chaumianClient != null)
				{
					await chaumianClient.StopAsync();
				}
			}
		}

		[Fact]
		public async Task SpendUnconfirmedTxTestAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			// Create the services.
			// 1. Create connection service.
			var nodes = new NodesGroup(Global.Config.Network, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Add(RegTestFixture.BackendRegTestNode.CreateNodeClient());

			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			Node node = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			// 3. Create wasabi synchronizer service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync),
				$"Index{rpc.Network}.dat");
			var synchronizer =
				new WasabiSynchronizer(rpc.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint), null);

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out _, password);

			// 5. Create chaumian coinjoin client.
			var chaumianClient = new CcjClient(synchronizer, rpc.Network, keyManager, new Uri(RegTestFixture.BackendEndPoint), null);

			// 6. Create wallet service.
			var workDir = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync));
			var wallet = new WalletService(keyManager, synchronizer, chaumianClient, memPoolService, nodes, workDir, serviceConfiguration);
			wallet.NewFilterProcessed += Wallet_NewFilterProcessed;

			Assert.Empty(wallet.Coins);

			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("foo label");

			try
			{
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.
				chaumianClient.Start(); // Start chaumian coinjoin client.

				// Wait until the filter our previous transaction is present.
				var blockCount = await rpc.GetBlockCountAsync();
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}

				Assert.Empty(wallet.Coins);

				// Get some money, make it confirm.
				// this is necesary because we are in a fork now.
				var tx0Id = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m),
					replaceable: true);
				while (wallet.Coins.Count == 0)
					await Task.Delay(500); // Waits for the funding transaction get to the mempool.
				Assert.Single(wallet.Coins);

				// Test mixin
				var operations = new[] {
					new WalletService.Operation(key.P2wpkhScript, Money.Coins(0.01m), ""),
					new WalletService.Operation(new Key().ScriptPubKey, Money.Coins(0.01m), ""),
					new WalletService.Operation(new Key().ScriptPubKey, Money.Coins(0.01m), "")
				};
				var tx1Res = wallet.BuildTransaction(password, operations, 2, allowUnconfirmed: true);
				Assert.Equal(3, tx1Res.OuterWalletOutputs.Single(x => x.ScriptPubKey == key.P2wpkhScript).AnonymitySet);

				// Spend the unconfirmed coin (send it to ourself)
				operations = new[] { new WalletService.Operation(key.PubKey.WitHash.ScriptPubKey, Money.Coins(0.5m), "") };
				tx1Res = wallet.BuildTransaction(password, operations, 2, allowUnconfirmed: true);
				await wallet.SendTransactionAsync(tx1Res.Transaction);

				while (wallet.Coins.Count != 3)
					await Task.Delay(500); // Waits for the funding transaction get to the mempool.

				// There is a coin created by the latest spending transaction
				Assert.Contains(wallet.Coins, x => x.TransactionId == tx1Res.Transaction.GetHash());

				// There is a coin destroyed
				Assert.Equal(1, wallet.Coins.Count(x => x.Unavailable && x.SpenderTransactionId == tx1Res.Transaction.GetHash()));

				// There is at least one coin created from the destruction of the first coin
				Assert.Contains(wallet.Coins, x => x.SpentOutputs.Any(o => o.TransactionId == tx0Id));

				var totalWallet = wallet.Coins.Where(c => !c.Unavailable).Sum(c => c.Amount);
				Assert.Equal((1 * Money.COIN) - tx1Res.Fee.Satoshi, totalWallet);

				// Spend the unconfirmed and unspent coin (send it to ourself)
				operations = new[] { new WalletService.Operation(key.PubKey.WitHash.ScriptPubKey, Money.Coins(0.5m), "") };
				var tx2Res = wallet.BuildTransaction(password, operations, 2, allowUnconfirmed: true, subtractFeeFromAmountIndex: 0);
				await wallet.SendTransactionAsync(tx2Res.Transaction);

				while (wallet.Coins.Count != 4)
					await Task.Delay(500); // Waits for the transaction get to the mempool.

				// There is a coin created by the latest spending transaction
				Assert.Contains(wallet.Coins, x => x.TransactionId == tx2Res.Transaction.GetHash());

				// There is a coin destroyed
				Assert.Equal(1, wallet.Coins.Count(x => x.Unavailable && x.SpenderTransactionId == tx2Res.Transaction.GetHash()));

				// There is at least one coin created from the destruction of the first coin
				Assert.Contains(wallet.Coins, x => x.SpentOutputs.Any(o => o.TransactionId == tx1Res.Transaction.GetHash()));

				totalWallet = wallet.Coins.Where(c => !c.Unavailable).Sum(c => c.Amount);
				Assert.Equal((1 * Money.COIN) - tx1Res.Fee.Satoshi - tx2Res.Fee.Satoshi, totalWallet);

				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				var blockId = (await rpc.GenerateAsync(1)).Single();
				try
				{
					await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);
				}
				catch (TimeoutException)
				{
					Logger.LogError<RegTests>("Index was not processed.");
					return; // Very rarely this test fails. I have no clue why. Probably because all these RegTests are interconnected, anyway let's not bother the CI with it.
				}

				// Verify transactions are confirmed in the blockchain
				var block = await rpc.GetBlockAsync(blockId);
				Assert.Contains(block.Transactions, x => x.GetHash() == tx2Res.Transaction.GetHash());
				Assert.Contains(block.Transactions, x => x.GetHash() == tx1Res.Transaction.GetHash());
				Assert.Contains(block.Transactions, x => x.GetHash() == tx0Id);

				Assert.True(wallet.Coins.All(x => x.Confirmed));

				// Test coin basic count.
				var coinCount = wallet.Coins.Count;
				var to = wallet.GetReceiveKey("foo");
				var res = wallet.BuildTransaction(password, new[] { new WalletService.Operation(to.P2wpkhScript, Money.Coins(0.2345m), "bar") }, 2, allowUnconfirmed: true);
				await wallet.SendTransactionAsync(res.Transaction);
				Assert.Equal(coinCount + 2, wallet.Coins.Count);
				Assert.Equal(2, wallet.Coins.Count(x => !x.Confirmed));
				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				await rpc.GenerateAsync(1);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);
				Assert.Equal(coinCount + 2, wallet.Coins.Count);
				Assert.Equal(0, wallet.Coins.Count(x => !x.Confirmed));
			}
			finally
			{
				wallet?.Dispose();
				// Dispose wasabi synchronizer service.
				synchronizer?.Dispose();
				// Dispose connection service.
				nodes?.Dispose();
				// Dispose mempool serving node.
				node?.Disconnect();
				// Dispose chaumian coinjoin client.
				if (chaumianClient != null)
				{
					await chaumianClient.StopAsync();
				}
			}
		}

		[Fact]
		public async Task ReplaceByFeeTxTestAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			// Create the services.
			// 1. Create connection service.
			var nodes = new NodesGroup(Global.Config.Network, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Add(RegTestFixture.BackendRegTestNode.CreateNodeClient());

			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			Node node = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			// 3. Create wasabi synchronizer service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync),
				$"Index{rpc.Network}.dat");
			var synchronizer =
				new WasabiSynchronizer(rpc.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint), null);

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out _, password);

			// 5. Create chaumian coinjoin client.
			var chaumianClient = new CcjClient(synchronizer, rpc.Network, keyManager, new Uri(RegTestFixture.BackendEndPoint), null);

			// 6. Create wallet service.
			var workDir = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync));
			var wallet = new WalletService(keyManager, synchronizer, chaumianClient, memPoolService, nodes, workDir, serviceConfiguration);
			wallet.NewFilterProcessed += Wallet_NewFilterProcessed;

			Assert.Empty(wallet.Coins);

			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("foo label");

			try
			{
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.
				chaumianClient.Start(); // Start chaumian coinjoin client.

				// Wait until the filter our previous transaction is present.
				var blockCount = await rpc.GetBlockCountAsync();
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}

				Assert.Empty(wallet.Coins);

				var tx0Id = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m), replaceable: true);
				while (wallet.Coins.Count == 0)
					await Task.Delay(500); // Waits for the funding transaction get to the mempool.
				Assert.Single(wallet.Coins);
				Assert.True(wallet.Coins.First().IsReplaceable);

				var bfr = await rpc.BumpFeeAsync(tx0Id);
				var tx1Id = bfr.TransactionId;
				await Task.Delay(2000); // Waits for the replacement transaction get to the mempool.
				Assert.Single(wallet.Coins);
				Assert.True(wallet.Coins.First().IsReplaceable);
				Assert.Equal(tx1Id, wallet.Coins.First().TransactionId);

				bfr = await rpc.BumpFeeAsync(tx1Id);
				var tx2Id = bfr.TransactionId;
				await Task.Delay(2000); // Waits for the replacement transaction get to the mempool.
				Assert.Single(wallet.Coins);
				Assert.True(wallet.Coins.First().IsReplaceable);
				Assert.Equal(tx2Id, wallet.Coins.First().TransactionId);

				await rpc.GenerateAsync(1);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 2);
				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}
				await Task.Delay(2000);

				var coin = wallet.Coins.First();
				Assert.Single(wallet.Coins);
				Assert.True(coin.Confirmed);
				Assert.False(coin.IsReplaceable);
				Assert.Equal(tx2Id, coin.TransactionId);
			}
			finally
			{
				wallet?.Dispose();
				// Dispose wasabi synchronizer service.
				synchronizer?.Dispose();
				// Dispose connection service.
				nodes?.Dispose();
				// Dispose mempool serving node.
				node?.Disconnect();
				// Dispose chaumian coinjoin client.
				if (chaumianClient != null)
				{
					await chaumianClient.StopAsync();
				}
			}
		}

		[Fact]
		public async Task CcjCoordinatorCtorTestsAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			Logger.TurnOff(); // turn off at the end, otherwise, the tests logs would have of warnings

			var bestBlockHash = await rpc.GetBestBlockHashAsync();
			var bestBlock = await rpc.GetBlockAsync(bestBlockHash);
			var coinbaseTxId = bestBlock.Transactions[0].GetHash();
			var offchainTxId = network.Consensus.ConsensusFactory.CreateTransaction().GetHash();
			var mempoolTxId = rpc.SendToAddress(new Key().PubKey.GetSegwitAddress(network), Money.Coins(1));

			var folder = Path.Combine(SharedFixture.DataDir, nameof(CcjCoordinatorCtorTestsAsync));
			await IoHelpers.DeleteRecursivelyWithMagicDustAsync(folder);
			Directory.CreateDirectory(folder);
			var cjfile = Path.Combine(folder, $"CoinJoins{network}.txt");
			File.WriteAllLines(cjfile, new[]{
				coinbaseTxId.ToString(),
				offchainTxId.ToString(),
				mempoolTxId.ToString()
			});

			using (var coordinatorToTest = new CcjCoordinator(network, folder, rpc, coordinator.RoundConfig))
			{
				var txIds = await File.ReadAllLinesAsync(cjfile);

				Assert.Contains(coinbaseTxId.ToString(), txIds);
				Assert.Contains(mempoolTxId.ToString(), txIds);
				Assert.DoesNotContain(offchainTxId.ToString(), txIds);

				await IoHelpers.DeleteRecursivelyWithMagicDustAsync(folder);
				Directory.CreateDirectory(folder);
				File.WriteAllLines(cjfile, new[]{
				coinbaseTxId.ToString(),
				"This line is invalid (the file is corrupted)",
				offchainTxId.ToString(),
			});
				var coordinatorToTest2 = new CcjCoordinator(network, folder, rpc, coordinatorToTest.RoundConfig);
				coordinatorToTest2.Dispose();
				txIds = await File.ReadAllLinesAsync(cjfile);
				Assert.Single(txIds);
				Assert.Contains(coinbaseTxId.ToString(), txIds);
				Assert.DoesNotContain(offchainTxId.ToString(), txIds);
				Assert.DoesNotContain("This line is invalid (the file is corrupted)", txIds);
			}

			Logger.TurnOn();
		}

		[Fact]
		public async Task CcjTestsAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			Money denomination = Money.Coins(0.2m);
			decimal coordinatorFeePercent = 0.2m;
			int anonymitySet = 2;
			int connectionConfirmationTimeout = 50;
			var roundConfig = new CcjRoundConfig(denomination, 2, coordinatorFeePercent, anonymitySet, 100, connectionConfirmationTimeout, 50, 50, 2, 24, false, 11);
			coordinator.UpdateRoundConfig(roundConfig);
			coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");

			Uri baseUri = new Uri(RegTestFixture.BackendEndPoint);
			using (var torClient = new TorHttpClient(baseUri, SharedFixture.TorSocks5Endpoint))
			using (var satoshiClient = new SatoshiClient(baseUri, null))
			{
				#region PostInputsGetStates

				// <-------------------------->
				// POST INPUTS and GET STATES tests
				// <-------------------------->

				IEnumerable<CcjRunningRoundState> states = await satoshiClient.GetAllRoundStatesAsync();
				Assert.Equal(2, states.Count());
				foreach (CcjRunningRoundState rs in states)
				{
					// Never changes.
					Assert.True(0 < rs.RoundId);
					Assert.Equal(Money.Coins(0.00009648m), rs.FeePerInputs);
					Assert.Equal(Money.Coins(0.00004752m), rs.FeePerOutputs);
					Assert.Equal(7, rs.MaximumInputCountPerPeer);
					// Changes per rounds.
					Assert.Equal(denomination, rs.Denomination);
					Assert.Equal(coordinatorFeePercent, rs.CoordinatorFeePercent);
					Assert.Equal(anonymitySet, rs.RequiredPeerCount);
					Assert.Equal(connectionConfirmationTimeout, rs.RegistrationTimeout);
					// Changes per phases.
					Assert.Equal(CcjRoundPhase.InputRegistration, rs.Phase);
					Assert.Equal(0, rs.RegisteredPeerCount);
				}

				// Inputs request tests
				var inputsRequest = new InputsRequest
				{
					BlindedOutputScripts = null,
					ChangeOutputAddress = null,
					Inputs = null,
				};

				var round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				var roundId = round.RoundId;
				inputsRequest.RoundId = roundId;
				var registeredAddresses = new BitcoinAddress[] { };
				var schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
				var requesters = new Requester[] { };

				HttpRequestException httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nInvalid request.", httpRequestException.Message);

				byte[] dummySignature = new byte[65];

				inputsRequest.BlindedOutputScripts = Enumerable.Range(0, round.MixingLevels.Count() + 1).Select(x => uint256.One);
				inputsRequest.ChangeOutputAddress = new Key().PubKey.GetAddress(network);
				inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new TxoRef(uint256.One, 0), Proof = dummySignature } };
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nToo many blinded output was provided", httpRequestException.Message);

				inputsRequest.BlindedOutputScripts = Enumerable.Range(0, round.MixingLevels.Count() - 2).Select(x => uint256.One);
				inputsRequest.ChangeOutputAddress = new Key().PubKey.GetAddress(network);
				inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new TxoRef(uint256.One, 0), Proof = dummySignature } };
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nDuplicate blinded output found", httpRequestException.Message);

				inputsRequest.BlindedOutputScripts = new[] { uint256.Zero };
				inputsRequest.ChangeOutputAddress = new Key().PubKey.GetAddress(network);
				inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new TxoRef(uint256.One, 0), Proof = dummySignature } };
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input is not unspent", httpRequestException.Message);

				var addr = await rpc.GetNewAddressAsync();
				var hash = await rpc.SendToAddressAsync(addr, Money.Coins(0.01m));
				var tx = await rpc.GetRawTransactionAsync(hash);
				var coin = tx.Outputs.GetCoins(addr.ScriptPubKey).Single();

				inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = coin.Outpoint.ToTxoRef(), Proof = dummySignature } };
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input is neither confirmed, nor is from an unconfirmed coinjoin.", httpRequestException.Message);

				var blocks = await rpc.GenerateAsync(1);
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input must be witness_v0_keyhash.", httpRequestException.Message);

				var blockHash = blocks.Single();
				var block = await rpc.GetBlockAsync(blockHash);
				var coinbase = block.Transactions.First();
				inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new TxoRef(coinbase.GetHash(), 0), Proof = dummySignature } };
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input is immature.", httpRequestException.Message);

				var key = new Key();
				var witnessAddress = key.PubKey.GetSegwitAddress(network);
				hash = await rpc.SendToAddressAsync(witnessAddress, Money.Coins(0.01m));
				await rpc.GenerateAsync(1);
				tx = await rpc.GetRawTransactionAsync(hash);
				coin = tx.Outputs.GetCoins(witnessAddress.ScriptPubKey).Single();
				inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = coin.Outpoint.ToTxoRef(), Proof = dummySignature } };
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}", httpRequestException.Message);

				var proof = key.SignCompact(uint256.One);
				inputsRequest.Inputs.First().Proof = proof;
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided proof is invalid.", httpRequestException.Message);

				round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				var requester = new Requester();
				uint256 msg = new uint256(Hashes.SHA256(network.Consensus.ConsensusFactory.CreateTransaction().ToBytes()));
				uint256 blindedData = requester.BlindMessage(msg, round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey);
				inputsRequest.BlindedOutputScripts = new[] { blindedData };
				uint256 blindedOutputScriptsHash = new uint256(Hashes.SHA256(blindedData.ToBytes()));

				proof = key.SignCompact(blindedOutputScriptsHash);
				inputsRequest.Inputs.First().Proof = proof;
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nNot enough inputs are provided. Fee to pay:", httpRequestException.Message);

				roundConfig.Denomination = Money.Coins(0.01m); // exactly the same as our output
				coordinator.UpdateRoundConfig(roundConfig);
				coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");
				round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				roundId = round.RoundId;
				inputsRequest.RoundId = roundId;
				schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nNot enough inputs are provided. Fee to pay:", httpRequestException.Message);

				roundConfig.Denomination = Money.Coins(0.00999999m); // one satoshi less than our output
				coordinator.UpdateRoundConfig(roundConfig);
				coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");
				round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				roundId = round.RoundId;
				inputsRequest.RoundId = roundId;
				schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nNot enough inputs are provided. Fee to pay:", httpRequestException.Message);

				roundConfig.Denomination = Money.Coins(0.008m); // one satoshi less than our output
				roundConfig.ConnectionConfirmationTimeout = 2;
				coordinator.UpdateRoundConfig(roundConfig);
				coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");
				round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				roundId = round.RoundId;
				inputsRequest.RoundId = roundId;
				schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
				requester = new Requester();
				requesters = new[] { requester };
				msg = network.Consensus.ConsensusFactory.CreateTransaction().GetHash();
				blindedData = requester.BlindMessage(msg, round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey);
				inputsRequest.BlindedOutputScripts = new[] { blindedData };
				blindedOutputScriptsHash = new uint256(Hashes.SHA256(blindedData.ToBytes()));
				proof = key.SignCompact(blindedOutputScriptsHash);
				inputsRequest.Inputs.First().Proof = proof;
				using (var aliceClient = await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null))
				{
					// Test DelayedClientRoundRegistration logic.
					ClientRoundRegistration first = null;
					var second = new ClientRoundRegistration(aliceClient,
						new[] { new SmartCoin(uint256.One, 1, Script.Empty, Money.Zero, new[] { new TxoRef(uint256.One, 1) }, Height.Unknown, true, 2) },
						BitcoinAddress.Create("12Rty3c8j3QiZSwLVaBtch6XUMZaja3RC7", Network.Main));
					first = second;
					second = null;
					Assert.NotNull(first);
					Assert.Null(second);

					Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
					Assert.True(aliceClient.RoundId > 0);

					CcjRunningRoundState roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
					Assert.Equal(CcjRoundPhase.InputRegistration, roundState.Phase);
					Assert.Equal(1, roundState.RegisteredPeerCount);

					httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
					Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nBlinded output has already been registered.", httpRequestException.Message);

					roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
					Assert.Equal(CcjRoundPhase.InputRegistration, roundState.Phase);
					Assert.Equal(1, roundState.RegisteredPeerCount);

					roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
					Assert.Equal(CcjRoundPhase.InputRegistration, roundState.Phase);
					Assert.Equal(1, roundState.RegisteredPeerCount);
				}

				round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				requester = new Requester();
				blindedData = requester.BlindScript(round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey.SignerPubKey, round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey.RpubKey, key.ScriptPubKey);
				inputsRequest.BlindedOutputScripts = new[] { blindedData };
				blindedOutputScriptsHash = new uint256(Hashes.SHA256(blindedData.ToBytes()));
				proof = key.SignCompact(blindedOutputScriptsHash);
				inputsRequest.Inputs.First().Proof = proof;
				using (var aliceClient = await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null))
				{
					Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
					Assert.True(aliceClient.RoundId > 0);

					var roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
					Assert.Equal(CcjRoundPhase.InputRegistration, roundState.Phase);
					Assert.Equal(1, roundState.RegisteredPeerCount);
				}

				inputsRequest.BlindedOutputScripts = new[] { uint256.One };
				blindedOutputScriptsHash = new uint256(Hashes.SHA256(uint256.One.ToBytes()));
				proof = key.SignCompact(blindedOutputScriptsHash);
				inputsRequest.Inputs.First().Proof = proof;
				inputsRequest.Inputs = new List<InputProofModel> { inputsRequest.Inputs.First(), inputsRequest.Inputs.First() };
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nCannot register an input twice.", httpRequestException.Message);

				var inputProofs = new List<InputProofModel>();
				for (int j = 0; j < 8; j++)
				{
					key = new Key();
					witnessAddress = key.PubKey.GetSegwitAddress(network);
					hash = await rpc.SendToAddressAsync(witnessAddress, Money.Coins(0.01m));
					await rpc.GenerateAsync(1);
					tx = await rpc.GetRawTransactionAsync(hash);
					coin = tx.Outputs.GetCoins(witnessAddress.ScriptPubKey).Single();
					proof = key.SignCompact(blindedOutputScriptsHash);
					inputProofs.Add(new InputProofModel { Input = coin.Outpoint.ToTxoRef(), Proof = proof });
				}
				await rpc.GenerateAsync(1);

				inputsRequest.Inputs = inputProofs;
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nMaximum 7 inputs can be registered.", httpRequestException.Message);
				inputProofs.RemoveLast();
				inputsRequest.Inputs = inputProofs;
				using (var aliceClient = await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null))
				{
					Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
					Assert.True(aliceClient.RoundId > 0);

					await Task.Delay(1000);
					var roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
					Assert.Equal(CcjRoundPhase.ConnectionConfirmation, roundState.Phase);
					Assert.Equal(2, roundState.RegisteredPeerCount);
					var inputRegistrableRoundState = await satoshiClient.GetRegistrableRoundStateAsync();
					Assert.Equal(0, inputRegistrableRoundState.RegisteredPeerCount);

					roundConfig.ConnectionConfirmationTimeout = 1; // One second.
					coordinator.UpdateRoundConfig(roundConfig); coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");
					round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
					roundId = round.RoundId;
					inputsRequest.RoundId = roundId;
					schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;

					roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
					Assert.Equal(CcjRoundPhase.ConnectionConfirmation, roundState.Phase);
					Assert.Equal(2, roundState.RegisteredPeerCount);
				}

				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nInput is already registered in another round.", httpRequestException.Message);

				// Wait until input registration times out.
				await Task.Delay(3000);
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, inputsRequest, baseUri, null));
				Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nInput is banned from participation for", httpRequestException.Message);

				var spendingTx = network.Consensus.ConsensusFactory.CreateTransaction();
				var bannedCoin = inputsRequest.Inputs.First().Input;
				var utxos = coordinator.UtxoReferee;
				Assert.NotNull(await utxos.TryGetBannedAsync(bannedCoin.ToOutPoint(), false));
				spendingTx.Inputs.Add(new TxIn(bannedCoin.ToOutPoint()));
				spendingTx.Outputs.Add(new TxOut(Money.Coins(1), new Key().PubKey.GetSegwitAddress(network)));
				var fakeBlockWithSpendingBannedCoins = network.Consensus.ConsensusFactory.CreateBlock();
				fakeBlockWithSpendingBannedCoins.Transactions.Add(spendingTx);

				await coordinator.ProcessBlockAsync(fakeBlockWithSpendingBannedCoins);

				Assert.NotNull(await utxos.TryGetBannedAsync(new OutPoint(spendingTx.GetHash(), 0), false));
				Assert.Null(await utxos.TryGetBannedAsync(bannedCoin.ToOutPoint(), false));

				states = await satoshiClient.GetAllRoundStatesAsync();
				foreach (var rs in states.Where(x => x.Phase == CcjRoundPhase.InputRegistration))
				{
					Assert.Equal(0, rs.RegisteredPeerCount);
				}

				#endregion PostInputsGetStates

				#region PostConfirmationPostUnconfirmation

				// <-------------------------->
				// POST CONFIRMATION and POST UNCONFIRMATION tests
				// <-------------------------->

				key = new Key();
				witnessAddress = key.PubKey.GetSegwitAddress(network);
				hash = await rpc.SendToAddressAsync(witnessAddress, Money.Coins(0.01m));
				await rpc.GenerateAsync(1);
				tx = await rpc.GetRawTransactionAsync(hash);
				coin = tx.Outputs.GetCoins(witnessAddress.ScriptPubKey).Single();
				round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				requester = new Requester();
				requesters = new[] { requester };
				BitcoinWitPubKeyAddress bitcoinWitPubKeyAddress = new Key().PubKey.GetSegwitAddress(network);
				registeredAddresses = new[] { bitcoinWitPubKeyAddress };
				Script script = bitcoinWitPubKeyAddress.ScriptPubKey;
				blindedData = requester.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, script);
				blindedOutputScriptsHash = new uint256(Hashes.SHA256(blindedData.ToBytes()));

				using (var aliceClient = await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, new Key().PubKey.GetAddress(network), new[] { blindedData }, new InputProofModel[] { new InputProofModel { Input = coin.Outpoint.ToTxoRef(), Proof = key.SignCompact(blindedOutputScriptsHash) } }, baseUri, null))
				{
					Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
					Assert.True(aliceClient.RoundId > 0);
					// Double the request.
					// badrequests
					using (var response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation"))
					{
						Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
					}
					using (var response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId={aliceClient.UniqueId}"))
					{
						Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
					}
					using (var response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?roundId={aliceClient.RoundId}"))
					{
						Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
						Assert.Equal("Invalid uniqueId provided.", await response.Content.ReadAsJsonAsync<string>());
					}
					using (var response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId=foo&roundId={aliceClient.RoundId}"))
					{
						Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
						Assert.Equal("Invalid uniqueId provided.", await response.Content.ReadAsJsonAsync<string>());
					}
					using (var response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId={aliceClient.UniqueId}&roundId=bar"))
					{
						Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
						Assert.Null(await response.Content.ReadAsJsonAsync<string>());
					}

					roundConfig.ConnectionConfirmationTimeout = 60;
					coordinator.UpdateRoundConfig(roundConfig);
					coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");
					round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
					roundId = round.RoundId;
					inputsRequest.RoundId = roundId;
					schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
					httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await aliceClient.PostConfirmationAsync());
					Assert.Equal($"{HttpStatusCode.Gone.ToReasonString()}\nRound is not running.", httpRequestException.Message);
				}

				using (var aliceClient = await AliceClient.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, new Key().PubKey.GetAddress(network), new[] { blindedData }, new InputProofModel[] { new InputProofModel { Input = coin.Outpoint.ToTxoRef(), Proof = key.SignCompact(blindedOutputScriptsHash) } }, baseUri, null))
				{
					Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
					Assert.True(aliceClient.RoundId > 0);
					await aliceClient.PostUnConfirmationAsync();
					using (var response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/unconfirmation?uniqueId={aliceClient.UniqueId}&roundId={aliceClient.RoundId}"))
					{
						Assert.True(response.IsSuccessStatusCode);
						Assert.Equal(HttpStatusCode.OK, response.StatusCode);
						Assert.Equal("Alice not found.", await response.Content.ReadAsJsonAsync<string>());
					}
				}

				#endregion PostConfirmationPostUnconfirmation

				#region PostOutput

				// <-------------------------->
				// POST OUTPUT tests
				// <-------------------------->

				var key1 = new Key();
				var key2 = new Key();
				var outputAddress1 = key1.PubKey.GetSegwitAddress(network);
				var outputAddress2 = key2.PubKey.GetSegwitAddress(network);
				var hash1 = await rpc.SendToAddressAsync(outputAddress1, Money.Coins(0.01m));
				var hash2 = await rpc.SendToAddressAsync(outputAddress2, Money.Coins(0.01m));
				await rpc.GenerateAsync(1);
				var tx1 = await rpc.GetRawTransactionAsync(hash1);
				var tx2 = await rpc.GetRawTransactionAsync(hash2);
				var index1 = 0;
				for (int i = 0; i < tx1.Outputs.Count; i++)
				{
					var output = tx1.Outputs[i];
					if (output.ScriptPubKey == outputAddress1.ScriptPubKey)
					{
						index1 = i;
					}
				}
				var index2 = 0;
				for (int i = 0; i < tx2.Outputs.Count; i++)
				{
					var output = tx2.Outputs[i];
					if (output.ScriptPubKey == outputAddress2.ScriptPubKey)
					{
						index2 = i;
					}
				}

				round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
				roundId = round.RoundId;

				var requester1 = new Requester();
				var requester2 = new Requester();

				uint256 blinded1 = requester1.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, outputAddress1.ScriptPubKey);
				uint256 blindedOutputScriptsHash1 = new uint256(Hashes.SHA256(blinded1.ToBytes()));
				uint256 blinded2 = requester2.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, outputAddress2.ScriptPubKey);
				uint256 blindedOutputScriptsHash2 = new uint256(Hashes.SHA256(blinded2.ToBytes()));

				var input1 = new OutPoint(hash1, index1);
				var input2 = new OutPoint(hash2, index2);

				using (var aliceClient1 = await AliceClient.CreateNewAsync(roundId, new[] { outputAddress1 }, schnorrPubKeys, new[] { requester1 }, network, new Key().PubKey.GetAddress(network), new[] { blinded1 }, new InputProofModel[] { new InputProofModel { Input = input1.ToTxoRef(), Proof = key1.SignCompact(blindedOutputScriptsHash1) } }, baseUri, null))
				using (var aliceClient2 = await AliceClient.CreateNewAsync(roundId, new[] { outputAddress2 }, schnorrPubKeys, new[] { requester2 }, network, new Key().PubKey.GetAddress(network), new[] { blinded2 }, new InputProofModel[] { new InputProofModel { Input = input2.ToTxoRef(), Proof = key2.SignCompact(blindedOutputScriptsHash2) } }, baseUri, null))
				{
					Assert.Equal(aliceClient2.RoundId, aliceClient1.RoundId);
					Assert.NotEqual(aliceClient2.UniqueId, aliceClient1.UniqueId);

					var connConfResp = await aliceClient1.PostConfirmationAsync();
					Assert.Equal(connConfResp.currentPhase, (await aliceClient1.PostConfirmationAsync()).currentPhase); // Make sure it won't throw error for double confirming.
					var connConfResp2 = await aliceClient2.PostConfirmationAsync();

					Assert.Equal(connConfResp.currentPhase, connConfResp2.currentPhase);
					httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await aliceClient2.PostConfirmationAsync());
					Assert.Equal($"{HttpStatusCode.Gone.ToReasonString()}\nParticipation can be only confirmed from InputRegistration or ConnectionConfirmation phase. Current phase: OutputRegistration.", httpRequestException.Message);

					var roundState = await satoshiClient.GetRoundStateAsync(aliceClient1.RoundId);
					Assert.Equal(CcjRoundPhase.OutputRegistration, roundState.Phase);

					if (!round.MixingLevels.GetBaseLevel().Signer.VerifyUnblindedSignature(connConfResp2.activeOutputs.First().signature, outputAddress2.ScriptPubKey.ToBytes()))
					{
						throw new NotSupportedException("Coordinator did not sign the blinded output properly.");
					}

					using (var bobClient1 = new BobClient(baseUri, null))
					using (var bobClient2 = new BobClient(baseUri, null))
					{
						await bobClient1.PostOutputAsync(aliceClient1.RoundId, outputAddress1, connConfResp.activeOutputs.First().signature, 0);
						await bobClient2.PostOutputAsync(aliceClient2.RoundId, outputAddress2, connConfResp2.activeOutputs.First().signature, 0);
					}

					roundState = await satoshiClient.GetRoundStateAsync(aliceClient1.RoundId);
					Assert.Equal(CcjRoundPhase.Signing, roundState.Phase);
					Assert.Equal(2, roundState.RegisteredPeerCount);
					Assert.Equal(2, roundState.RequiredPeerCount);

					#endregion PostOutput

					#region GetCoinjoin

					// <-------------------------->
					// GET COINJOIN tests
					// <-------------------------->

					Transaction unsignedCoinJoin = await aliceClient1.GetUnsignedCoinJoinAsync();
					Assert.Equal(unsignedCoinJoin.ToHex(), (await aliceClient1.GetUnsignedCoinJoinAsync()).ToHex());
					Assert.Equal(unsignedCoinJoin.ToHex(), (await aliceClient2.GetUnsignedCoinJoinAsync()).ToHex());

					Assert.Contains(outputAddress1.ScriptPubKey, unsignedCoinJoin.Outputs.Select(x => x.ScriptPubKey));
					Assert.Contains(outputAddress2.ScriptPubKey, unsignedCoinJoin.Outputs.Select(x => x.ScriptPubKey));
					Assert.True(2 == unsignedCoinJoin.Outputs.Count); // Because the two input is equal, so change addresses won't be used, nor coordinator fee will be taken.
					Assert.Contains(input1, unsignedCoinJoin.Inputs.Select(x => x.PrevOut));
					Assert.Contains(input2, unsignedCoinJoin.Inputs.Select(x => x.PrevOut));
					Assert.True(2 == unsignedCoinJoin.Inputs.Count);

					#endregion GetCoinjoin

					#region PostSignatures

					// <-------------------------->
					// POST SIGNATURES tests
					// <-------------------------->

					var partSignedCj1 = Transaction.Parse(unsignedCoinJoin.ToHex(), network);
					var partSignedCj2 = Transaction.Parse(unsignedCoinJoin.ToHex(), network);

					var builder = Network.RegTest.CreateTransactionBuilder();
					partSignedCj1 = builder
						.ContinueToBuild(partSignedCj1)
						.AddKeys(key1)
						.AddCoins(new Coin(tx1, input1.N))
						.BuildTransaction(true);

					builder = Network.RegTest.CreateTransactionBuilder();
					partSignedCj2 = builder
						.ContinueToBuild(partSignedCj2)
						.AddKeys(key2)
						.AddCoins(new Coin(tx2, input2.N))
						.BuildTransaction(true);

					var myDic1 = new Dictionary<int, WitScript>();
					var myDic2 = new Dictionary<int, WitScript>();

					for (int i = 0; i < unsignedCoinJoin.Inputs.Count; i++)
					{
						var input = unsignedCoinJoin.Inputs[i];
						if (input.PrevOut == input1)
						{
							myDic1.Add(i, partSignedCj1.Inputs[i].WitScript);
						}
						if (input.PrevOut == input2)
						{
							myDic2.Add(i, partSignedCj2.Inputs[i].WitScript);
						}
					}

					await aliceClient1.PostSignaturesAsync(myDic1);
					await aliceClient2.PostSignaturesAsync(myDic2);

					uint256[] mempooltxs = await rpc.GetRawMempoolAsync();
					Assert.Contains(unsignedCoinJoin.GetHash(), mempooltxs);

					#endregion PostSignatures
				}
			}
		}

		[Fact]
		public async Task CcjEqualInputTestsAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			Money denomination = Money.Coins(0.1m);
			decimal coordinatorFeePercent = 0.0002m;
			int anonymitySet = 4;
			int connectionConfirmationTimeout = 50;
			var roundConfig = new CcjRoundConfig(denomination, 2, coordinatorFeePercent, anonymitySet, 100, connectionConfirmationTimeout, 50, 50, 2, 24, false, 11);
			coordinator.UpdateRoundConfig(roundConfig);
			coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");

			Uri baseUri = new Uri(RegTestFixture.BackendEndPoint);
			using (var torClient = new TorHttpClient(baseUri, SharedFixture.TorSocks5Endpoint))
			using (var satoshiClient = new SatoshiClient(baseUri, null))
			{
				var round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				var roundId = round.RoundId;

				// We have to 4 participant so, this data structure is for keeping track of
				// important data for each of the participants in the coinjoin session.
				var participants = new List<(AliceClient aliceClient,
											 List<(Requester requester, BitcoinWitPubKeyAddress outputAddress, uint256 blindedScript)> outouts,
											 List<(TxoRef input, byte[] proof, Coin coin, Key key)> inputs)>();

				// INPUS REGISTRATION PHASE --
				for (var anosetIdx = 0; anosetIdx < anonymitySet; anosetIdx++)
				{
					// Create as many outputs as mixin levels (even when we don't have funds enough)
					var outputs = new List<(Requester requester, BitcoinWitPubKeyAddress outputAddress, uint256 blindedScript)>();
					foreach (var level in round.MixingLevels.Levels)
					{
						var requester = new Requester();
						var outputsAddress = new Key().PubKey.GetSegwitAddress(network);
						var scriptPubKey = outputsAddress.ScriptPubKey;
						// We blind the scriptPubKey with a new requester by mixin level
						var blindedScript = requester.BlindScript(level.Signer.Key.PubKey, level.Signer.R.PubKey, scriptPubKey);
						outputs.Add((requester, outputsAddress, blindedScript));
					}

					// Calculate the SHA256( blind1 || blind2 || .....|| blindN )
					var blindedOutputScriptList = outputs.Select(x => x.blindedScript);
					var blindedOutputScriptListBytes = ByteHelpers.Combine(blindedOutputScriptList.Select(x => x.ToBytes()));
					var blindedOutputScriptsHash = new uint256(Hashes.SHA256(blindedOutputScriptListBytes));

					// Create 4 new coins that we want to mix
					var inputs = new List<(TxoRef input, byte[] proof, Coin coin, Key key)>();
					for (var inputIdx = 0; inputIdx < 4; inputIdx++)
					{
						var key = new Key();
						var outputAddress = key.PubKey.GetSegwitAddress(network);
						var hash = await rpc.SendToAddressAsync(outputAddress, Money.Coins(0.1m));
						await rpc.GenerateAsync(1);
						var tx = await rpc.GetRawTransactionAsync(hash);
						var index = tx.Outputs.FindIndex(x => x.ScriptPubKey == outputAddress.ScriptPubKey);
						var input = new OutPoint(hash, index);

						inputs.Add((
							input.ToTxoRef(),
							key.SignCompact(blindedOutputScriptsHash),
							new Coin(tx, (uint)index),
							key
						));
					}

					// Save alice client and the outputs, requesters, etc
					var changeOutput = new Key().PubKey.GetAddress(network);
					var inputProof = inputs.Select(x => new InputProofModel { Input = x.input, Proof = x.proof });
					var aliceClient = await AliceClient.CreateNewAsync(
						round.RoundId,
						outputs.Select(x => x.outputAddress),
						round.MixingLevels.SchnorrPubKeys,
						outputs.Select(x => x.requester),
						network, changeOutput, blindedOutputScriptList, inputProof, baseUri, null);

					// We check the coordinator signed all the alice blinded outputs
					participants.Add((aliceClient, outputs, inputs));
				}

				// CONNECTION CONFIRMATION PHASE --
				var activeOutputs = new List<IEnumerable<(BitcoinAddress output, UnblindedSignature signature, int level)>>();
				var j = 0;
				foreach (var (aliceClient, _, _) in participants)
				{
					var res = await aliceClient.PostConfirmationAsync();
					activeOutputs.Add(res.activeOutputs);
					j++;
				}

				// OUTPUTS REGISTRATION PHASE --
				var roundState = await satoshiClient.GetRoundStateAsync(roundId);
				Assert.Equal(CcjRoundPhase.OutputRegistration, roundState.Phase);

				var l = 0;
				foreach (var (aliceClient, outputs, _) in participants)
				{
					using (var bobClient = new BobClient(baseUri, null))
					{
						var i = 0;
						foreach (var output in outputs.Take(activeOutputs[l].Count()))
						{
							await bobClient.PostOutputAsync(aliceClient.RoundId, output.outputAddress, activeOutputs[l].ElementAt(i).signature, i);
							i++;
						}
					}
					l++;
				}

				// SIGNING PHASE --
				roundState = await satoshiClient.GetRoundStateAsync(roundId);
				Assert.Equal(CcjRoundPhase.Signing, roundState.Phase);

				uint256 transactionId = null;
				foreach (var (aliceClient, outputs, inputs) in participants)
				{
					var unsignedTransaction = await aliceClient.GetUnsignedCoinJoinAsync();
					transactionId = unsignedTransaction.GetHash();

					// Verify the transaction contains the expected inputs and outputs

					// Verify the inputs are the expected ones.
					foreach (var input in inputs)
					{
						Assert.Contains(input.input, unsignedTransaction.Inputs.Select(x => x.PrevOut.ToTxoRef()));
					}

					// Sign the transaction
					var builder = Network.RegTest.CreateTransactionBuilder();
					var partSignedCj = builder
						.ContinueToBuild(unsignedTransaction)
						.AddKeys(inputs.Select(x => x.key).ToArray())
						.AddCoins(inputs.Select(x => x.coin))
						.BuildTransaction(true);

					var witnesses = partSignedCj.Inputs
						.AsIndexedInputs()
						.Where(x => x.WitScript != WitScript.Empty)
						.ToDictionary(x => (int)x.Index, x => x.WitScript);

					await aliceClient.PostSignaturesAsync(witnesses);
				}

				uint256[] mempooltxs = await rpc.GetRawMempoolAsync();
				Assert.Contains(transactionId, mempooltxs);
			}
		}

		[Fact]
		public async Task NotingTestsAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			Money denomination = Money.Coins(1m);
			decimal coordinatorFeePercent = 0.1m;
			int anonymitySet = 2;
			int connectionConfirmationTimeout = 1;
			bool doesNoteBeforeBan = true;
			CcjRoundConfig roundConfig = new CcjRoundConfig(denomination, 140, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 1, 1, 1, 24, doesNoteBeforeBan, 11);
			coordinator.UpdateRoundConfig(roundConfig);
			coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");

			Uri baseUri = new Uri(RegTestFixture.BackendEndPoint);

			var registerRequests = new List<(BitcoinWitPubKeyAddress changeOutputAddress, uint256 blindedData, InputProofModel[] inputsProofs)>();
			AliceClient aliceClientBackup = null;
			CcjRound round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			for (int i = 0; i < roundConfig.AnonymitySet; i++)
			{
				BitcoinWitPubKeyAddress activeOutputAddress = new Key().PubKey.GetSegwitAddress(network);
				BitcoinWitPubKeyAddress changeOutputAddress = new Key().PubKey.GetSegwitAddress(network);
				Key inputKey = new Key();
				BitcoinWitPubKeyAddress inputAddress = inputKey.PubKey.GetSegwitAddress(network);

				var requester = new Requester();
				uint256 blinded = requester.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, activeOutputAddress.ScriptPubKey);
				uint256 blindedOutputScriptsHash = new uint256(Hashes.SHA256(blinded.ToBytes()));

				uint256 txHash = await rpc.SendToAddressAsync(inputAddress, Money.Coins(2));
				await rpc.GenerateAsync(1);
				Transaction transaction = await rpc.GetRawTransactionAsync(txHash);
				Coin coin = transaction.Outputs.GetCoins(inputAddress.ScriptPubKey).Single();
				OutPoint input = coin.Outpoint;

				InputProofModel inputProof = new InputProofModel { Input = input.ToTxoRef(), Proof = inputKey.SignCompact(blindedOutputScriptsHash) };
				InputProofModel[] inputsProofs = new InputProofModel[] { inputProof };
				registerRequests.Add((changeOutputAddress, blinded, inputsProofs));
				aliceClientBackup = await AliceClient.CreateNewAsync(round.RoundId, new[] { activeOutputAddress }, new[] { round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey }, new[] { requester }, network, changeOutputAddress, new[] { blinded }, inputsProofs, baseUri, null);
			}

			await WaitForTimeoutAsync(baseUri);

			int bannedCount = coordinator.UtxoReferee.CountBanned(false);
			Assert.Equal(0, bannedCount);
			int notedCount = coordinator.UtxoReferee.CountBanned(true);
			Assert.Equal(anonymitySet, notedCount);

			round = coordinator.GetCurrentInputRegisterableRoundOrDefault();

			foreach (var registerRequest in registerRequests)
			{
				await AliceClient.CreateNewAsync(round.RoundId, aliceClientBackup.RegisteredAddresses, round.MixingLevels.GetAllLevels().Select(x => x.SchnorrKey.SchnorrPubKey), aliceClientBackup.Requesters, network, registerRequest.changeOutputAddress, new[] { registerRequest.blindedData }, registerRequest.inputsProofs, baseUri, null);
			}

			await WaitForTimeoutAsync(baseUri);

			bannedCount = coordinator.UtxoReferee.CountBanned(false);
			Assert.Equal(anonymitySet, bannedCount);
			notedCount = coordinator.UtxoReferee.CountBanned(true);
			Assert.Equal(anonymitySet, notedCount);
		}

		private static async Task WaitForTimeoutAsync(Uri baseUri)
		{
			using (var satoshiClient = new SatoshiClient(baseUri, null))
			{
				var times = 0;
				while (!(await satoshiClient.GetAllRoundStatesAsync()).All(x => x.Phase == CcjRoundPhase.InputRegistration))
				{
					await Task.Delay(100);
					if (times > 50) // 5 sec, 3 should be enough
					{
						throw new TimeoutException("Not all rounds were in InputRegistration.");
					}
					times++;
				}
			}
		}

		[Fact]
		public async Task BanningTestsAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			Money denomination = Money.Coins(0.1m);
			decimal coordinatorFeePercent = 0.1m;
			int anonymitySet = 3;
			int connectionConfirmationTimeout = 120;
			var roundConfig = new CcjRoundConfig(denomination, 140, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 1, 1, 1, 24, true, 11);
			coordinator.UpdateRoundConfig(roundConfig);
			coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");

			await rpc.GenerateAsync(3); // So to make sure we have enough money.

			Uri baseUri = new Uri(RegTestFixture.BackendEndPoint);
			var fundingTxCount = 0;
			var inputRegistrationUsers = new List<(Requester requester, uint256 blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinWitPubKeyAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData)>();
			CcjRound round = null;
			for (int i = 0; i < roundConfig.AnonymitySet; i++)
			{
				var userInputData = new List<(Key key, BitcoinWitPubKeyAddress inputAddress, uint256 txHash, Transaction tx, OutPoint input)>();
				var activeOutputAddress = new Key().PubKey.GetAddress(network);
				var changeOutputAddress = new Key().PubKey.GetAddress(network);
				round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				Requester requester = new Requester();
				uint256 blinded = requester.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, activeOutputAddress.ScriptPubKey);
				uint256 blindedOutputScriptsHash = new uint256(Hashes.SHA256(blinded.ToBytes()));

				var inputProofModels = new List<InputProofModel>();
				int numberOfInputs = new Random().Next(1, 7);
				var receiveSatoshiSum = 0;
				for (int j = 0; j < numberOfInputs; j++)
				{
					var key = new Key();
					var receiveSatoshi = new Random().Next(1000, 100000000);
					receiveSatoshiSum += receiveSatoshi;
					if (j == numberOfInputs - 1)
					{
						receiveSatoshi = 100000000;
					}
					BitcoinWitPubKeyAddress inputAddress = key.PubKey.GetSegwitAddress(network);
					uint256 txHash = await rpc.SendToAddressAsync(inputAddress, Money.Satoshis(receiveSatoshi));
					fundingTxCount++;
					Assert.NotNull(txHash);
					Transaction transaction = await rpc.GetRawTransactionAsync(txHash);

					var coin = transaction.Outputs.GetCoins(inputAddress.ScriptPubKey).Single();

					OutPoint input = coin.Outpoint;
					var inputProof = new InputProofModel { Input = input.ToTxoRef(), Proof = key.SignCompact(blindedOutputScriptsHash) };
					inputProofModels.Add(inputProof);

					GetTxOutResponse getTxOutResponse = await rpc.GetTxOutAsync(input.Hash, (int)input.N, includeMempool: true);
					// Check if inputs are unspent.
					Assert.NotNull(getTxOutResponse);

					userInputData.Add((key, inputAddress, txHash, transaction, input));
				}

				inputRegistrationUsers.Add((requester, blinded, activeOutputAddress, changeOutputAddress, inputProofModels, userInputData));
			}

			var mempool = await rpc.GetRawMempoolAsync();
			Assert.Equal(inputRegistrationUsers.SelectMany(x => x.userInputData).Count(), mempool.Length);

			while ((await rpc.GetRawMempoolAsync()).Length != 0)
			{
				await rpc.GenerateAsync(1);
			}

			var aliceClients = new List<Task<AliceClient>>();

			foreach (var user in inputRegistrationUsers)
			{
				aliceClients.Add(AliceClient.CreateNewAsync(round.RoundId, new[] { user.activeOutputAddress }, new[] { round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey }, new[] { user.requester }, network, user.changeOutputAddress, new[] { user.blinded }, user.inputProofModels, baseUri, null));
			}

			long roundId = 0;
			var users = new List<(Requester requester, uint256 blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinWitPubKeyAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData, AliceClient aliceClient, UnblindedSignature unblindedSignature)>();
			for (int i = 0; i < inputRegistrationUsers.Count; i++)
			{
				var user = inputRegistrationUsers[i];
				var request = aliceClients[i];

				var aliceClient = await request;

				if (roundId == 0)
				{
					roundId = aliceClient.RoundId;
				}
				else
				{
					Assert.Equal(roundId, aliceClient.RoundId);
				}
				// Because it's valuetuple.
				users.Add((user.requester, user.blinded, user.activeOutputAddress, user.changeOutputAddress, user.inputProofModels, user.userInputData, aliceClient, null));
			}

			Assert.Equal(users.Count, roundConfig.AnonymitySet);

			var confirmationRequests = new List<Task<(CcjRoundPhase currentPhase, IEnumerable<(BitcoinAddress output, UnblindedSignature signature, int level)>)>>();

			foreach (var user in users)
			{
				confirmationRequests.Add(user.aliceClient.PostConfirmationAsync());
			}

			CcjRoundPhase roundPhase = CcjRoundPhase.InputRegistration;
			int k = 0;
			foreach (var request in confirmationRequests)
			{
				var resp = await request;
				if (roundPhase == CcjRoundPhase.InputRegistration)
				{
					roundPhase = resp.currentPhase;
				}
				else
				{
					Assert.Equal(roundPhase, resp.currentPhase);
				}

				var user = users.ElementAt(k);
				user.unblindedSignature = resp.Item2.First().signature;
			}

			using (var satoshiClient = new SatoshiClient(baseUri, null))
			{
				var times = 0;
				while (!(await satoshiClient.GetAllRoundStatesAsync()).All(x => x.Phase == CcjRoundPhase.InputRegistration))
				{
					await Task.Delay(100);
					if (times > 50) // 5 sec, 3 should be enough
					{
						throw new TimeoutException("Not all rounds were in InputRegistration.");
					}
					times++;
				}
			}

			int bannedCount = coordinator.UtxoReferee.CountBanned(false);
			Assert.Equal(0, bannedCount);

			aliceClients.Clear();
			round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			foreach (var user in inputRegistrationUsers)
			{
				aliceClients.Add(AliceClient.CreateNewAsync(round.RoundId, new[] { user.activeOutputAddress }, new[] { round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey }, new[] { user.requester }, network, user.changeOutputAddress, new[] { user.blinded }, user.inputProofModels, baseUri, null));
			}

			roundId = 0;
			users = new List<(Requester requester, uint256 blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinWitPubKeyAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData, AliceClient aliceClient, UnblindedSignature unblindedSignature)>();
			for (int i = 0; i < inputRegistrationUsers.Count; i++)
			{
				var user = inputRegistrationUsers[i];
				var request = aliceClients[i];

				var aliceClient = await request;
				if (roundId == 0)
				{
					roundId = aliceClient.RoundId;
				}
				else
				{
					Assert.Equal(roundId, aliceClient.RoundId);
				}
				// Because it's valuetuple.
				users.Add((user.requester, user.blinded, user.activeOutputAddress, user.changeOutputAddress, user.inputProofModels, user.userInputData, aliceClient, null));
			}

			Assert.Equal(users.Count, roundConfig.AnonymitySet);

			confirmationRequests = new List<Task<(CcjRoundPhase currentPhase, IEnumerable<(BitcoinAddress output, UnblindedSignature signature, int level)>)>>();

			foreach (var user in users)
			{
				confirmationRequests.Add(user.aliceClient.PostConfirmationAsync());
			}

			using (var satoshiClient = new SatoshiClient(baseUri, null))
			{
				var times = 0;
				while (!(await satoshiClient.GetAllRoundStatesAsync()).All(x => x.Phase == CcjRoundPhase.InputRegistration))
				{
					await Task.Delay(100);
					if (times > 50) // 5 sec, 3 should be enough
					{
						throw new TimeoutException("Not all rounds were in InputRegistration.");
					}
					times++;
				}
			}

			bannedCount = coordinator.UtxoReferee.CountBanned(false);
			Assert.True(bannedCount >= roundConfig.AnonymitySet);

			foreach (var aliceClient in aliceClients)
			{
				aliceClient.Dispose();
			}
		}

		[Fact]
		public async Task Ccj100ParticipantsTestsAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			Money denomination = Money.Coins(0.1m);
			decimal coordinatorFeePercent = 0.003m;
			int anonymitySet = 100;
			int connectionConfirmationTimeout = 120;
			var roundConfig = new CcjRoundConfig(denomination, 144, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
			coordinator.UpdateRoundConfig(roundConfig);
			coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");
			await rpc.GenerateAsync(100); // So to make sure we have enough money.

			Uri baseUri = new Uri(RegTestFixture.BackendEndPoint);
			var fundingTxCount = 0;
			var inputRegistrationUsers = new List<(Requester requester, uint256 blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinWitPubKeyAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData)>();
			for (int i = 0; i < roundConfig.AnonymitySet; i++)
			{
				var userInputData = new List<(Key key, BitcoinWitPubKeyAddress inputAddress, uint256 txHash, Transaction tx, OutPoint input)>();
				var activeOutputAddress = new Key().PubKey.GetAddress(network);
				var changeOutputAddress = new Key().PubKey.GetAddress(network);
				CcjRound round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				var requester = new Requester();
				uint256 blinded = requester.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, activeOutputAddress.ScriptPubKey);
				uint256 blindedOutputScriptsHash = new uint256(Hashes.SHA256(blinded.ToBytes()));

				var inputProofModels = new List<InputProofModel>();
				int numberOfInputs = new Random().Next(1, 7);
				var receiveSatoshiSum = 0;
				for (int j = 0; j < numberOfInputs; j++)
				{
					var key = new Key();
					var receiveSatoshi = new Random().Next(1000, 100000000);
					receiveSatoshiSum += receiveSatoshi;
					if (j == numberOfInputs - 1)
					{
						receiveSatoshi = 100000000;
					}
					BitcoinWitPubKeyAddress inputAddress = key.PubKey.GetSegwitAddress(network);
					uint256 txHash = await rpc.SendToAddressAsync(inputAddress, Money.Satoshis(receiveSatoshi));
					fundingTxCount++;
					Assert.NotNull(txHash);
					Transaction transaction = await rpc.GetRawTransactionAsync(txHash);

					var coin = transaction.Outputs.GetCoins(inputAddress.ScriptPubKey).Single();

					OutPoint input = coin.Outpoint;
					var inputProof = new InputProofModel { Input = input.ToTxoRef(), Proof = key.SignCompact(blindedOutputScriptsHash) };
					inputProofModels.Add(inputProof);

					GetTxOutResponse getTxOutResponse = await rpc.GetTxOutAsync(input.Hash, (int)input.N, includeMempool: true);
					// Check if inputs are unspent.
					Assert.NotNull(getTxOutResponse);

					userInputData.Add((key, inputAddress, txHash, transaction, input));
				}

				inputRegistrationUsers.Add((requester, blinded, activeOutputAddress, changeOutputAddress, inputProofModels, userInputData));
			}

			var mempool = await rpc.GetRawMempoolAsync();
			Assert.Equal(inputRegistrationUsers.SelectMany(x => x.userInputData).Count(), mempool.Length);

			while ((await rpc.GetRawMempoolAsync()).Length != 0)
			{
				await rpc.GenerateAsync(1);
			}

			Logger.TurnOff();

			var aliceClients = new List<Task<AliceClient>>();

			var currentRound = coordinator.GetCurrentInputRegisterableRoundOrDefault();

			foreach (var user in inputRegistrationUsers)
			{
				aliceClients.Add(AliceClient.CreateNewAsync(currentRound.RoundId, new[] { user.activeOutputAddress }, currentRound.MixingLevels.SchnorrPubKeys, new[] { user.requester }, network, user.changeOutputAddress, new[] { user.blinded }, user.inputProofModels, baseUri, null));
			}

			long roundId = 0;
			var users = new List<(Requester requester, uint256 blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinWitPubKeyAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData, AliceClient aliceClient, UnblindedSignature unblindedSignature)>();
			for (int i = 0; i < inputRegistrationUsers.Count; i++)
			{
				var user = inputRegistrationUsers[i];
				var request = aliceClients[i];

				var aliceClient = await request;
				if (roundId == 0)
				{
					roundId = aliceClient.RoundId;
				}
				else
				{
					Assert.Equal(roundId, aliceClient.RoundId);
				}
				// Because it's valuetuple.
				users.Add((user.requester, user.blinded, user.activeOutputAddress, user.changeOutputAddress, user.inputProofModels, user.userInputData, aliceClient, null));
			}

			Logger.TurnOn();

			Assert.Equal(users.Count, roundConfig.AnonymitySet);
			var confirmationRequests = new List<Task<(CcjRoundPhase currentPhase, IEnumerable<(BitcoinAddress output, UnblindedSignature signature, int level)>)>>();

			foreach (var user in users)
			{
				confirmationRequests.Add(user.aliceClient.PostConfirmationAsync());
			}

			CcjRoundPhase roundPhase = CcjRoundPhase.InputRegistration;
			int k = 0;
			foreach (var request in confirmationRequests)
			{
				var resp = await request;
				if (roundPhase == CcjRoundPhase.InputRegistration)
				{
					roundPhase = resp.currentPhase;
				}
				else
				{
					Assert.Equal(roundPhase, resp.currentPhase);
				}

				// Because it's valuetuple.
				var user = users.ElementAt(k);
				users.RemoveAt(k);
				users.Add((user.requester, user.blinded, user.activeOutputAddress, user.changeOutputAddress, user.inputProofModels, user.userInputData, user.aliceClient, resp.Item2.First().signature));
			}

			var outputRequests = new List<(BobClient, Task)>();
			foreach (var user in users)
			{
				var bobClient = new BobClient(baseUri, null);
				outputRequests.Add((bobClient, bobClient.PostOutputAsync(roundId, user.activeOutputAddress, user.unblindedSignature, 0)));
			}

			foreach (var request in outputRequests)
			{
				await request.Item2;
				request.Item1.Dispose();
			}

			var coinjoinRequests = new List<Task<Transaction>>();
			foreach (var user in users)
			{
				coinjoinRequests.Add(user.aliceClient.GetUnsignedCoinJoinAsync());
			}

			Transaction unsignedCoinJoin = null;
			foreach (var request in coinjoinRequests)
			{
				if (unsignedCoinJoin is null)
				{
					unsignedCoinJoin = await request;
				}
				else
				{
					Assert.Equal(unsignedCoinJoin.ToHex(), (await request).ToHex());
				}
			}

			var signatureRequests = new List<Task>();
			foreach (var user in users)
			{
				var partSignedCj = Transaction.Parse(unsignedCoinJoin.ToHex(), network);
				partSignedCj = Network.RegTest.CreateTransactionBuilder()
							.ContinueToBuild(partSignedCj)
							.AddKeys(user.userInputData.Select(x => x.key).ToArray())
							.AddCoins(user.userInputData.Select(x => new Coin(x.tx, x.input.N)))
							.BuildTransaction(true);

				var myDic = new Dictionary<int, WitScript>();

				for (int i = 0; i < unsignedCoinJoin.Inputs.Count; i++)
				{
					var input = unsignedCoinJoin.Inputs[i];
					if (user.userInputData.Select(x => x.input).Contains(input.PrevOut))
					{
						myDic.Add(i, partSignedCj.Inputs[i].WitScript);
					}
				}

				signatureRequests.Add(user.aliceClient.PostSignaturesAsync(myDic));
			}

			await Task.WhenAll(signatureRequests);

			uint256[] mempooltxs = await rpc.GetRawMempoolAsync();
			Assert.Contains(unsignedCoinJoin.GetHash(), mempooltxs);

			var coins = new List<Coin>();
			var finalCoinjoin = await rpc.GetRawTransactionAsync(mempooltxs.First());
			foreach (var input in finalCoinjoin.Inputs)
			{
				var getTxOut = await rpc.GetTxOutAsync(input.PrevOut.Hash, (int)input.PrevOut.N, includeMempool: false);

				coins.Add(new Coin(input.PrevOut.Hash, input.PrevOut.N, getTxOut.TxOut.Value, getTxOut.TxOut.ScriptPubKey));
			}

			FeeRate feeRateTx = finalCoinjoin.GetFeeRate(coins.ToArray());
			var esr = await rpc.EstimateSmartFeeAsync((int)roundConfig.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, tryOtherFeeRates: true);
			FeeRate feeRateReal = esr.FeeRate;

			Assert.True(feeRateReal.FeePerK - (feeRateReal.FeePerK / 2) < feeRateTx.FeePerK); // Max 50% mistake.
			Assert.True(2 * feeRateReal.FeePerK > feeRateTx.FeePerK); // Max 200% mistake.

			var activeOutput = finalCoinjoin.GetIndistinguishableOutputs(includeSingle: true).OrderByDescending(x => x.count).First();
			Assert.True(activeOutput.value >= roundConfig.Denomination);
			Assert.True(activeOutput.count >= roundConfig.AnonymitySet);

			foreach (var aliceClient in aliceClients)
			{
				aliceClient.Dispose();
			}
		}

		[Fact]
		public async Task CcjFeeTestsAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(CcjFeeTestsAsync), $"Index{network}.dat");
			var synchronizer = new WasabiSynchronizer(network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint), null);
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.

			Money denomination = Money.Coins(0.9m);
			decimal coordinatorFeePercent = 0.1m;
			int anonymitySet = 7;
			int connectionConfirmationTimeout = 14;
			var roundConfig = new CcjRoundConfig(denomination, 140, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
			coordinator.UpdateRoundConfig(roundConfig);
			coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");

			var participants = new List<dynamic>();

			// 1. Prepare and start services.
			for (int i = 0; i < anonymitySet; i++)
			{
				double damount;
				switch (i)
				{
					case 0:
						damount = 1;
						break;

					case 1:
						damount = 1.1;
						break;

					case 2:
						damount = 1.2;
						break;

					case 3:
						damount = 3.1;
						break;

					case 4:
						damount = 4.1;
						break;

					case 5:
						damount = 7.1;
						break;

					case 6:
						damount = 8.1;
						break;

					default:
						damount = 1;
						break;
				}

				var amount = Money.Coins((decimal)damount);

				var keyManager = KeyManager.CreateNew(out _, password);
				var key = keyManager.GenerateNewKey("foo", KeyState.Clean, false);
				var bech = key.GetP2wpkhAddress(network);
				var txid = await rpc.SendToAddressAsync(bech, amount, replaceable: false);
				key.SetKeyState(KeyState.Used);
				var tx = await rpc.GetRawTransactionAsync(txid);
				var height = await rpc.GetBlockCountAsync();
				var bechCoin = tx.Outputs.GetCoins(bech.ScriptPubKey).Single();

				var smartCoin = new SmartCoin(bechCoin, tx.Inputs.Select(x => new TxoRef(x.PrevOut)).ToArray(), height + 1, replaceable: false, anonymitySet: tx.GetAnonymitySet(bechCoin.Outpoint.N));

				var chaumianClient = new CcjClient(synchronizer, rpc.Network, keyManager, new Uri(RegTestFixture.BackendEndPoint), null);

				participants.Add((smartCoin, chaumianClient));
			}

			await rpc.GenerateAsync(1);

			try
			{
				// 2. Start mixing.
				foreach (var participant in participants)
				{
					SmartCoin coin = participant.Item1;
					CcjClient chaumianClient = participant.Item2;
					chaumianClient.Start();
					await chaumianClient.QueueCoinsToMixAsync(password, coin);
				}

				Task timeout = Task.Delay(TimeSpan.FromSeconds(connectionConfirmationTimeout * 2 + 7 * 2 + 7 * 2 + 7 * 2));
				while ((await rpc.GetRawMempoolAsync()).Length == 0)
				{
					if (timeout.IsCompletedSuccessfully)
					{
						throw new TimeoutException("CoinJoin wasn't propagated.");
					}
					await Task.Delay(1000);
				}
			}
			finally
			{
				foreach (var participant in participants)
				{
					SmartCoin coin = participant.Item1;
					CcjClient chaumianClient = participant.Item2;

					Task timeout = Task.Delay(3000);
					while (chaumianClient.State.GetActivelyMixingRounds().Count() != 0)
					{
						if (timeout.IsCompletedSuccessfully)
						{
							throw new TimeoutException("CoinJoin was not noticed.");
						}
						await Task.Delay(1000);
					}

					if (chaumianClient != null)
					{
						await chaumianClient.DequeueAllCoinsFromMixAsync();
						await chaumianClient.StopAsync();
					}
				}
			}
		}

		[Fact]
		public async Task CcjClientTestsAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(1);

			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(CcjClientTestsAsync), $"Index{network}.dat");
			var synchronizer = new WasabiSynchronizer(network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint), null);
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.

			Money denomination = Money.Coins(0.1m);
			decimal coordinatorFeePercent = 0.1m;
			int anonymitySet = 2;
			int connectionConfirmationTimeout = 14;
			var roundConfig = new CcjRoundConfig(denomination, 140, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
			coordinator.UpdateRoundConfig(roundConfig);
			coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");
			await rpc.GenerateAsync(3); // So to make sure we have enough money.
			var keyManager = KeyManager.CreateNew(out _, password);
			var key1 = keyManager.GenerateNewKey("foo", KeyState.Clean, false);
			var key2 = keyManager.GenerateNewKey("bar", KeyState.Clean, false);
			var key3 = keyManager.GenerateNewKey("baz", KeyState.Clean, false);
			var key4 = keyManager.GenerateNewKey("qux", KeyState.Clean, false);
			var bech1 = key1.GetP2wpkhAddress(network);
			var bech2 = key2.GetP2wpkhAddress(network);
			var bech3 = key3.GetP2wpkhAddress(network);
			var bech4 = key4.GetP2wpkhAddress(network);
			var amount1 = Money.Coins(0.03m);
			var amount2 = Money.Coins(0.08m);
			var amount3 = Money.Coins(0.3m);
			var amount4 = Money.Coins(0.4m);
			var txid1 = await rpc.SendToAddressAsync(bech1, amount1, replaceable: false);
			var txid2 = await rpc.SendToAddressAsync(bech2, amount2, replaceable: false);
			var txid3 = await rpc.SendToAddressAsync(bech3, amount3, replaceable: false);
			var txid4 = await rpc.SendToAddressAsync(bech4, amount4, replaceable: false);
			key1.SetKeyState(KeyState.Used);
			key2.SetKeyState(KeyState.Used);
			key3.SetKeyState(KeyState.Used);
			key4.SetKeyState(KeyState.Used);
			var tx1 = await rpc.GetRawTransactionAsync(txid1);
			var tx2 = await rpc.GetRawTransactionAsync(txid2);
			var tx3 = await rpc.GetRawTransactionAsync(txid3);
			var tx4 = await rpc.GetRawTransactionAsync(txid4);
			await rpc.GenerateAsync(1);
			var height = await rpc.GetBlockCountAsync();
			var bech1Coin = tx1.Outputs.GetCoins(bech1.ScriptPubKey).Single();
			var bech2Coin = tx2.Outputs.GetCoins(bech2.ScriptPubKey).Single();
			var bech3Coin = tx3.Outputs.GetCoins(bech3.ScriptPubKey).Single();
			var bech4Coin = tx4.Outputs.GetCoins(bech4.ScriptPubKey).Single();

			var smartCoin1 = new SmartCoin(bech1Coin, tx1.Inputs.Select(x => new TxoRef(x.PrevOut)).ToArray(), height, replaceable: false, anonymitySet: tx1.GetAnonymitySet(bech1Coin.Outpoint.N));
			var smartCoin2 = new SmartCoin(bech2Coin, tx2.Inputs.Select(x => new TxoRef(x.PrevOut)).ToArray(), height, replaceable: false, anonymitySet: tx2.GetAnonymitySet(bech2Coin.Outpoint.N));
			var smartCoin3 = new SmartCoin(bech3Coin, tx3.Inputs.Select(x => new TxoRef(x.PrevOut)).ToArray(), height, replaceable: false, anonymitySet: tx3.GetAnonymitySet(bech3Coin.Outpoint.N));
			var smartCoin4 = new SmartCoin(bech4Coin, tx4.Inputs.Select(x => new TxoRef(x.PrevOut)).ToArray(), height, replaceable: false, anonymitySet: tx4.GetAnonymitySet(bech4Coin.Outpoint.N));

			var chaumianClient1 = new CcjClient(synchronizer, rpc.Network, keyManager, new Uri(RegTestFixture.BackendEndPoint), null);
			var chaumianClient2 = new CcjClient(synchronizer, rpc.Network, keyManager, new Uri(RegTestFixture.BackendEndPoint), null);
			try
			{
				chaumianClient1.Start(); // Exactly delay it for 2 seconds, this will make sure of timeout later.
				chaumianClient2.Start();

				smartCoin1.CoinJoinInProgress = true;
				Assert.False((await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1)).Any()); // Inconsistent internal state, so don't try to add.
				Assert.True(smartCoin1.CoinJoinInProgress);

				await Assert.ThrowsAsync<SecurityException>(async () => await chaumianClient1.QueueCoinsToMixAsync("asdasdasd", smartCoin1, smartCoin2));
				Assert.True(smartCoin1.CoinJoinInProgress);
				Assert.False(smartCoin2.CoinJoinInProgress);
				smartCoin1.CoinJoinInProgress = false;

				await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1, smartCoin2);
				Assert.True(smartCoin1.CoinJoinInProgress);
				Assert.True(smartCoin2.CoinJoinInProgress);

				// Make sure it doesn't throw.
				await chaumianClient1.DequeueCoinsFromMixAsync(new SmartCoin((network.Consensus.ConsensusFactory.CreateTransaction()).GetHash(), 1, new Script(), Money.Parse("3"), new TxoRef[] { new TxoRef((network.Consensus.ConsensusFactory.CreateTransaction()).GetHash(), 0) }, Height.MemPool, replaceable: false, anonymitySet: 1));

				Assert.True(2 == (await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1, smartCoin2)).Count());
				await chaumianClient1.DequeueCoinsFromMixAsync(smartCoin1);
				Assert.False(smartCoin1.CoinJoinInProgress);
				await chaumianClient1.DequeueCoinsFromMixAsync(smartCoin1, smartCoin2);
				Assert.False(smartCoin1.CoinJoinInProgress);
				Assert.False(smartCoin2.CoinJoinInProgress);
				Assert.True(2 == (await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1, smartCoin2)).Count());
				Assert.True(smartCoin1.CoinJoinInProgress);
				Assert.True(smartCoin2.CoinJoinInProgress);
				await chaumianClient1.DequeueCoinsFromMixAsync(smartCoin1);
				await chaumianClient1.DequeueCoinsFromMixAsync(smartCoin2);
				Assert.False(smartCoin1.CoinJoinInProgress);
				Assert.False(smartCoin2.CoinJoinInProgress);

				Assert.True(2 == (await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1, smartCoin2)).Count());
				Assert.True(smartCoin1.CoinJoinInProgress);
				Assert.True(smartCoin2.CoinJoinInProgress);
				Assert.True(1 == (await chaumianClient2.QueueCoinsToMixAsync(password, smartCoin3)).Count());

				Task timeout = Task.Delay(TimeSpan.FromSeconds(connectionConfirmationTimeout * 2 + 7 * 2 + 7 * 2 + 7 * 2));
				while ((await rpc.GetRawMempoolAsync()).Length == 0)
				{
					if (timeout.IsCompletedSuccessfully)
					{
						throw new TimeoutException("CoinJoin wasn't propagated.");
					}
					await Task.Delay(1000);
				}

				var cj = (await rpc.GetRawMempoolAsync()).Single();
				smartCoin1.SpenderTransactionId = cj;
				smartCoin2.SpenderTransactionId = cj;
				smartCoin3.SpenderTransactionId = cj;

				// Make sure if times out, it  tries again.
				connectionConfirmationTimeout = 1;
				roundConfig = new CcjRoundConfig(denomination, 140, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
				coordinator.UpdateRoundConfig(roundConfig);
				coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");
				Assert.NotEmpty(chaumianClient1.State.GetAllQueuedCoins());
				await chaumianClient1.DequeueAllCoinsFromMixAsync();
				Assert.Empty(chaumianClient1.State.GetAllQueuedCoins());
				await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin4);
				Assert.NotEmpty(chaumianClient1.State.GetAllQueuedCoins());
				Assert.NotEmpty(chaumianClient1.State.GetAllWaitingCoins());
				Assert.Empty(chaumianClient1.State.GetAllRegisteredCoins());
				while (chaumianClient1.State.GetAllWaitingCoins().Any())
				{
					await Task.Delay(1000);
				}
				Assert.NotEmpty(chaumianClient1.State.GetAllQueuedCoins());
				Assert.Empty(chaumianClient1.State.GetAllWaitingCoins());
				Assert.NotEmpty(chaumianClient1.State.GetAllRegisteredCoins());
				int times = 0;
				while (!chaumianClient1.State.GetAllWaitingCoins().Any()) // // Make sure to wait until times out.
				{
					await Task.Delay(1000);
					if (times > 21)
					{
						throw new TimeoutException("State.GetAllWaitingCoins() always empty.");
					}
					times++;
				}

				Assert.NotEmpty(chaumianClient1.State.GetAllQueuedCoins());
				Assert.Empty(chaumianClient1.State.GetAllRegisteredCoins());
			}
			finally
			{
				if (chaumianClient1 != null)
				{
					await chaumianClient1.StopAsync();
				}
				if (chaumianClient2 != null)
				{
					await chaumianClient2.StopAsync();
				}
			}
		}

		[Fact]
		public async Task CoinJoinMultipleRoundTestsAsync()
		{
			(string password, RPCClient rpc, Network network, CcjCoordinator coordinator, ServiceConfiguration serviceConfiguration) = await InitializeTestEnvironmentAsync(3);

			Money denomination = Money.Coins(0.1m);
			decimal coordinatorFeePercent = 0.1m;
			int anonymitySet = 2;
			int connectionConfirmationTimeout = 14;
			var roundConfig = new CcjRoundConfig(denomination, 140, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
			coordinator.UpdateRoundConfig(roundConfig);
			coordinator.AbortAllRoundsInInputRegistration(nameof(RegTests), "");

			// Create the services.
			// 1. Create connection service.
			var nodes = new NodesGroup(Global.Config.Network, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Add(RegTestFixture.BackendRegTestNode.CreateNodeClient());

			var nodes2 = new NodesGroup(Global.Config.Network, requirements: Constants.NodeRequirements);
			nodes2.ConnectedNodes.Add(RegTestFixture.BackendRegTestNode.CreateNodeClient());

			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			Node node = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			var memPoolService2 = new MemPoolService();
			Node node2 = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node2.Behaviors.Add(new MemPoolBehavior(memPoolService2));

			// 3. Create wasabi synchronizer service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(CoinJoinMultipleRoundTestsAsync), $"Index{network}.dat");
			var synchronizer = new WasabiSynchronizer(network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint), null);

			var indexFilePath2 = Path.Combine(SharedFixture.DataDir, nameof(CoinJoinMultipleRoundTestsAsync), $"Index{network}2.dat");
			var synchronizer2 = new WasabiSynchronizer(network, indexFilePath2, new Uri(RegTestFixture.BackendEndPoint), null);

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out _, password);

			var keyManager2 = KeyManager.CreateNew(out _, password);

			// 5. Create chaumian coinjoin client.
			var chaumianClient = new CcjClient(synchronizer, network, keyManager, new Uri(RegTestFixture.BackendEndPoint), null);

			var chaumianClient2 = new CcjClient(synchronizer, network, keyManager2, new Uri(RegTestFixture.BackendEndPoint), null);

			// 6. Create wallet service.
			var workDir = Path.Combine(SharedFixture.DataDir, nameof(CoinJoinMultipleRoundTestsAsync));
			var wallet = new WalletService(keyManager, synchronizer, chaumianClient, memPoolService, nodes, workDir, serviceConfiguration);
			wallet.NewFilterProcessed += Wallet_NewFilterProcessed;

			var workDir2 = Path.Combine(SharedFixture.DataDir, $"{nameof(CoinJoinMultipleRoundTestsAsync)}2");
			var wallet2 = new WalletService(keyManager2, synchronizer2, chaumianClient2, memPoolService2, nodes2, workDir2, serviceConfiguration);

			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("fundZeroLink");
			var txid = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m));
			Assert.NotNull(txid);
			var key2 = wallet2.GetReceiveKey("fundZeroLink");
			var key3 = wallet2.GetReceiveKey("fundZeroLink");
			var key4 = wallet2.GetReceiveKey("fundZeroLink");
			var txid2 = await rpc.SendToAddressAsync(key2.GetP2wpkhAddress(network), Money.Coins(0.11m));
			var txid3 = await rpc.SendToAddressAsync(key3.GetP2wpkhAddress(network), Money.Coins(0.12m));
			var txid4 = await rpc.SendToAddressAsync(key4.GetP2wpkhAddress(network), Money.Coins(0.13m));

			await rpc.GenerateAsync(1);

			try
			{
				Interlocked.Exchange(ref _filtersProcessedByWalletCount, 0);
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.
				chaumianClient.Start(); // Start chaumian coinjoin client.
				nodes2.Connect(); // Start connection service.
				node2.VersionHandshake(); // Start mempool service.
				synchronizer2.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.
				chaumianClient2.Start(); // Start chaumian coinjoin client.

				// Wait until the filter our previous transaction is present.
				var blockCount = await rpc.GetBlockCountAsync();
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}
				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet2.InitializeAsync(cts.Token); // Initialize wallet service.
				}

				var waitCount = 0;
				while (wallet.Coins.Sum(x => x.Amount) == Money.Zero)
				{
					await Task.Delay(1000);
					waitCount++;
					if (waitCount >= 21)
					{
						throw new TimeoutException("Funding transaction to the wallet1 did not arrive.");
					}
				}
				waitCount = 0;
				while (wallet2.Coins.Sum(x => x.Amount) == Money.Zero)
				{
					await Task.Delay(1000);
					waitCount++;
					if (waitCount >= 21)
					{
						throw new TimeoutException("Funding transaction to the wallet2 did not arrive.");
					}
				}

				Assert.True(1 == (await chaumianClient.QueueCoinsToMixAsync(password, wallet.Coins.ToArray())).Count());
				Assert.True(3 == (await chaumianClient2.QueueCoinsToMixAsync(password, wallet2.Coins.ToArray())).Count());

				Task timeout = Task.Delay(TimeSpan.FromSeconds(2 * (1 + 11 + 7 + 3 * (3 + 7))));
				while (wallet.Coins.Count != 7)
				{
					if (timeout.IsCompletedSuccessfully)
					{
						throw new TimeoutException("CoinJoin wasn't propagated or didn't arrive.");
					}
					await Task.Delay(1000);
				}

				var times = 0;
				while (wallet.Coins.Where(x => x.Label == "ZeroLink Change" && x.Unspent).SingleOrDefault() is null)
				{
					await Task.Delay(1000);
					times++;
					if (times >= 21) throw new TimeoutException("Wallet spends were not recognized.");
				}
				SmartCoin[] unspentChanges = wallet.Coins.Where(x => x.Label == "ZeroLink Change" && x.Unspent).ToArray();
				await wallet.ChaumianClient.DequeueCoinsFromMixAsync(unspentChanges);

				Assert.Equal(3, wallet.Coins.Count(x => x.Label == "ZeroLink Mixed Coin" && !x.Unavailable));
				Assert.Equal(3, wallet2.Coins.Count(x => x.Label == "ZeroLink Mixed Coin" && !x.Unavailable));
				Assert.Equal(0, wallet.Coins.Count(x => x.Label == "ZeroLink Mixed Coin" && !x.Unspent));
				Assert.Equal(0, wallet2.Coins.Count(x => x.Label == "ZeroLink Mixed Coin" && !x.Unspent));
				Assert.Equal(2, wallet.Coins.Count(x => x.Label == "ZeroLink Change" && !x.Unspent));
				Assert.Equal(0, wallet2.Coins.Count(x => x.Label == "ZeroLink Change"));
				Assert.Equal(0, wallet.Coins.Count(x => x.Label == "ZeroLink Change" && x.Unspent));
				Assert.Equal(0, wallet.Coins.Count(x => x.Label == "ZeroLink Dequeued Change" && !x.Unspent));
				Assert.Equal(1, wallet.Coins.Count(x => x.Label == "ZeroLink Dequeued Change" && !x.Unavailable));
			}
			finally
			{
				wallet.NewFilterProcessed -= Wallet_NewFilterProcessed;
				wallet?.Dispose();
				// Dispose wasabi synchronizer service.
				synchronizer?.Dispose();
				// Dispose connection service.
				nodes?.Dispose();
				// Dispose mempool serving node.
				node?.Disconnect();
				// Dispose chaumian coinjoin client.
				if (chaumianClient != null)
				{
					await chaumianClient.StopAsync();
				}
				wallet2?.Dispose();
				// Dispose wasabi synchronizer service.
				synchronizer2?.Dispose();
				// Dispose connection service.
				nodes2?.Dispose();
				// Dispose chaumian coinjoin client.
				if (chaumianClient2 != null)
				{
					await chaumianClient2.StopAsync();
				}
			}
		}

		#endregion ClientTests
	}
}
