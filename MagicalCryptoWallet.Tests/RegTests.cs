using MagicalCryptoWallet.Backend;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.KeyManagement;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using MagicalCryptoWallet.Services;
using MagicalCryptoWallet.Tests.NodeBuilding;
using MagicalCryptoWallet.TorSocks5;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
	public class RegTests : IClassFixture<SharedFixture>
	{
		private SharedFixture Fixture { get; }

		public RegTests(SharedFixture fixture)
		{
			Fixture = fixture;

			if (Fixture.BackendNodeBuilder == null)
			{
				Fixture.BackendNodeBuilder = NodeBuilder.CreateAsync().GetAwaiter().GetResult();
				Fixture.BackendNodeBuilder.CreateNodeAsync().GetAwaiter().GetResult();;
				Fixture.BackendNodeBuilder.StartAllAsync().GetAwaiter().GetResult();;
				Fixture.BackendRegTestNode = Fixture.BackendNodeBuilder.Nodes[0];
				Fixture.BackendRegTestNode.Generate(101);
				var rpc = Fixture.BackendRegTestNode.CreateRpcClient();

				var authString = rpc.Authentication.Split(':');
				Global.InitializeAsync(rpc.Network, authString[0], authString[1], rpc).GetAwaiter().GetResult();

				Fixture.BackendEndPoint = $"http://localhost:{new Random().Next(37130, 38000)}/";
				Fixture.BackendHost = WebHost.CreateDefaultBuilder()
					.UseStartup<Startup>()
					.UseUrls(Fixture.BackendEndPoint)
					.Build();
				var hostInitializationTask = Fixture.BackendHost.RunAsync();
				Logger.LogInfo<SharedFixture>($"Started Backend webhost: {Fixture.BackendEndPoint}");

				var delayTask = Task.Delay(3000);
				Task.WaitAny(delayTask, hostInitializationTask); // Wait for server to initialize (Without this OSX CI will fail)
			}
		}

		private async Task AssertFiltersInitializedAsync()
		{
			var firstHash = await Global.RpcClient.GetBlockHashAsync(0);
			while (true)
			{
				using (var client = new TorHttpClient(new Uri(Fixture.BackendEndPoint)))
				using (var response = await client.SendAsync(HttpMethod.Get, "/api/v1/btc/Blockchain/filters/" + firstHash))
				{
					Assert.Equal(HttpStatusCode.OK, response.StatusCode);
					var filters = await response.Content.ReadAsJsonAsync<List<string>>();
					var filterCount = filters.Count();
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

		#region BackendTests

		[Fact]
		public async void GetExchangeRatesAsyncAsync()
		{
			using (var client = new TorHttpClient(new Uri(Fixture.BackendEndPoint)))
			using (var response = await client.SendAsync(HttpMethod.Get, "/api/v1/btc/Blockchain/exchange-rates"))
			{
				Assert.True(response.IsSuccessStatusCode);

				var exchangeRates = await response.Content.ReadAsJsonAsync<List<ExchangeRate>>();
				Assert.Single(exchangeRates);

				var rate = exchangeRates[0];
				Assert.Equal("USD", rate.Ticker);
				Assert.True(rate.Rate > 0);
			}
		}

		[Fact]
		public async void BroadcastWithOutMinFeeAsync()
		{
			await Global.RpcClient.GenerateAsync(1);
			var utxos = await Global.RpcClient.ListUnspentAsync();
			var utxo = utxos[0];
			var addr = await Global.RpcClient.GetNewAddressAsync();
			var tx = new Transaction();
			tx.Inputs.Add(new TxIn(utxo.OutPoint, Script.Empty));
			tx.Outputs.Add(new TxOut(utxo.Amount, addr));
			var signedTx = await Global.RpcClient.SignRawTransactionAsync(tx);

			var content = new StringContent($"'{signedTx.ToHex()}'", Encoding.UTF8, "application/json");
			using (var client = new TorHttpClient(new Uri(Fixture.BackendEndPoint)))
			using (var response = await client.SendAsync(HttpMethod.Post, "/api/v1/btc/Blockchain/broadcast", content))
			{

				Assert.False(response.IsSuccessStatusCode);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			}
		}

		[Fact]
		public async void BroadcastReplayTxAsync()
		{
			await Global.RpcClient.GenerateAsync(1);
			var utxos = await Global.RpcClient.ListUnspentAsync();
			var utxo = utxos[0];
			var tx = await Global.RpcClient.GetRawTransactionAsync(utxo.OutPoint.Hash);
			var content = new StringContent($"'{tx.ToHex()}'", Encoding.UTF8, "application/json");
			using (var client = new TorHttpClient(new Uri(Fixture.BackendEndPoint)))
			using (var response = await client.SendAsync(HttpMethod.Post, "/api/v1/btc/Blockchain/broadcast", content))
			{
				Assert.True(response.IsSuccessStatusCode);
				Assert.Equal("\"Transaction is already in the blockchain.\"", await response.Content.ReadAsStringAsync());
			}
		}

		[Fact]
		public async void BroadcastInvalidTxAsync()
		{
			var content = new StringContent($"''", Encoding.UTF8, "application/json");
			using (var client = new TorHttpClient(new Uri(Fixture.BackendEndPoint)))
			using (var response = await client.SendAsync(HttpMethod.Post, "/api/v1/btc/Blockchain/broadcast", content))
			{
				Assert.False(response.IsSuccessStatusCode);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				Assert.Equal("\"Invalid hex.\"", await response.Content.ReadAsStringAsync());
			}
		}

		#endregion

		#region ServicesTests

		[Fact]
		public async Task MempoolAsync()
		{
			var memPoolService = new MemPoolService();
			Node node = Fixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));
			node.VersionHandshake();

			try
			{
				memPoolService.TransactionReceived += MempoolAsync_MemPoolService_TransactionReceived;

				// Using the interlocked, not because it makes sense in this context, but to
				// set an example that these values are often concurrency sensitive
				for (int i = 0; i < 10; i++)
				{
					var addr = await Global.RpcClient.GetNewAddressAsync();
					var res = await Global.RpcClient.SendToAddressAsync(addr, new Money(0.01m, MoneyUnit.BTC));
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
			Logger.LogDebug<P2pTests>($"Mempool transaction received: {e.GetHash()}.");
		}

		[Fact]
		public async Task FilterDownloaderTestAsync()
		{
			await AssertFiltersInitializedAsync();

			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(FilterDownloaderTestAsync), $"Index{Global.RpcClient.Network}.dat");

			var downloader = new IndexDownloader(Global.RpcClient.Network, indexFilePath, new Uri(Fixture.BackendEndPoint));
			try
			{
				downloader.Synchronize(requestInterval: TimeSpan.FromSeconds(1));

				// Test initial syncronization.

				var times = 0;
				int filterCount;
				while ((filterCount = downloader.GetFiltersIncluding(Network.RegTest.GenesisHash).Count()) < 102)
				{
					if (times > 500) // 30 sec
					{
						throw new TimeoutException($"{nameof(IndexDownloader)} test timed out. Needed filters: {102}, got only: {filterCount}.");
					}
					await Task.Delay(100);
					times++;
				}

				// Test later syncronization.
				Fixture.BackendRegTestNode.Generate(10);
				times = 0;
				while ((filterCount = downloader.GetFiltersIncluding(new Height(0)).Count()) < 112)
				{
					if (times > 500) // 30 sec
					{
						throw new TimeoutException($"{nameof(IndexDownloader)} test timed out. Needed filters: {112}, got only: {filterCount}.");
					}
					await Task.Delay(100);
					times++;
				}

				// Test correct number of filters is received.
				var hundredthHash = await Global.RpcClient.GetBlockHashAsync(100);
				Assert.Equal(100, downloader.GetFiltersIncluding(hundredthHash).First().BlockHeight.Value);

				// Test filter block hashes are correct.
				var filters = downloader.GetFiltersIncluding(Network.RegTest.GenesisHash).ToArray();
				for (int i = 0; i < 101; i++)
				{
					var expectedHash = await Global.RpcClient.GetBlockHashAsync(i);
					var filter = filters[i];
					Assert.Equal(i, filter.BlockHeight.Value);
					Assert.Equal(expectedHash, filter.BlockHash);
					Assert.Null(filter.Filter);
				}
			}
			finally
			{
				if (downloader != null)
				{
					await downloader.StopAsync();
				}
			}
		}

		[Fact]
		public async Task ReorgTestAsync()
		{			
			await AssertFiltersInitializedAsync();

			var network = Network.RegTest;
			var keyManager = KeyManager.CreateNew(out Mnemonic mnemonic, "password");

			// Mine some coins, make a few bech32 transactions then make it confirm.
			await Global.RpcClient.GenerateAsync(1);
			var key = keyManager.GenerateNewKey("", KeyState.Clean, isInternal: false);
			var tx2 = await Global.RpcClient.SendToAddressAsync(key.GetP2wpkhAddress(network), new Money(0.1m, MoneyUnit.BTC));
			key = keyManager.GenerateNewKey("", KeyState.Clean, isInternal: false);
			var tx3 = await Global.RpcClient.SendToAddressAsync(key.GetP2wpkhAddress(network), new Money(0.1m, MoneyUnit.BTC));
			var tx4 = await Global.RpcClient.SendToAddressAsync(key.GetP2pkhAddress(network), new Money(0.1m, MoneyUnit.BTC));
			var tx5 = await Global.RpcClient.SendToAddressAsync(key.GetP2shOverP2wpkhAddress(network), new Money(0.1m, MoneyUnit.BTC));
			var tx1Res = await Global.RpcClient.SendCommandAsync(RPCOperations.sendtoaddress, key.GetP2wpkhAddress(network).ToString(), new Money(0.1m, MoneyUnit.BTC).ToString(false, true), "", "", false, true);
			var tx1 = new uint256(tx1Res.ResultString);

			await Global.RpcClient.GenerateAsync(2); // Generate two, so we can test for two reorg

			_reorgTestAsync_ReorgCount = 0;

			var node = Fixture.BackendRegTestNode;
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(ReorgTestAsync), $"Index{Global.RpcClient.Network}.dat");

			var downloader = new IndexDownloader(Global.RpcClient.Network, indexFilePath, new Uri(Fixture.BackendEndPoint));
			try
			{
				downloader.Synchronize(requestInterval: TimeSpan.FromSeconds(3));

				downloader.Reorged += ReorgTestAsync_Downloader_Reorged;

				// Test initial syncronization.	
				await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(90), downloader, false);

				var indexLines = await File.ReadAllLinesAsync(indexFilePath);
				var lastFilter = indexLines.Last();
				var tip = await Global.RpcClient.GetBestBlockHashAsync();
				Assert.StartsWith(tip.ToString(), indexLines.Last());
				var tipBlock = await Global.RpcClient.GetBlockHeaderAsync(tip);
				Assert.Contains(tipBlock.HashPrevBlock.ToString(), indexLines.TakeLast(2).First());

				var utxoPath = Global.IndexBuilderService.Bech32UtxoSetFilePath;
				var utxoLines = await File.ReadAllTextAsync(utxoPath);
				Assert.Contains(tx1.ToString(), utxoLines);
				Assert.Contains(tx2.ToString(), utxoLines);
				Assert.Contains(tx3.ToString(), utxoLines);
				Assert.DoesNotContain(tx4.ToString(), utxoLines); // make sure only bech is recorded
				Assert.DoesNotContain(tx5.ToString(), utxoLines); // make sure only bech is recorded

				// Test syncronization after fork.
				await Global.RpcClient.InvalidateBlockAsync(tip); // Reorg 1
				tip = await Global.RpcClient.GetBestBlockHashAsync();
				await Global.RpcClient.InvalidateBlockAsync(tip); // Reorg 2
				var tx1bumpRes = await Global.RpcClient.SendCommandAsync("bumpfee", tx1.ToString()); // RBF it
				var tx1bump = new uint256(tx1bumpRes.Result["txid"].ToString());

				await Global.RpcClient.GenerateAsync(5);
				await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(90), downloader, false);

				utxoLines = await File.ReadAllTextAsync(utxoPath);
				Assert.Contains(tx1bump.ToString(), utxoLines); // assert the tx1bump is the correct tx
				Assert.DoesNotContain(tx1.ToString(), utxoLines); // assert tx1 is abandoned (despite it confirmed previously)
				Assert.Contains(tx2.ToString(), utxoLines);
				Assert.Contains(tx3.ToString(), utxoLines);
				Assert.DoesNotContain(tx4.ToString(), utxoLines);
				Assert.DoesNotContain(tx5.ToString(), utxoLines);

				indexLines = await File.ReadAllLinesAsync(indexFilePath);
				Assert.DoesNotContain(tip.ToString(), indexLines);
				Assert.DoesNotContain(tipBlock.HashPrevBlock.ToString(), indexLines);

				// Test filter block hashes are correct after fork.
				var filters = downloader.GetFiltersIncluding(Network.RegTest.GenesisHash).ToArray();
				var blockCountIncludingGenesis = await Global.RpcClient.GetBlockCountAsync() + 1;
				for (int i = 0; i < blockCountIncludingGenesis; i++)
				{
					var expectedHash = await Global.RpcClient.GetBlockHashAsync(i);
					var filter = filters[i];
					Assert.Equal(i, filter.BlockHeight.Value);
					Assert.Equal(expectedHash, filter.BlockHash);
					if (i < 101) // Later other tests may fill the filter.
					{
						Assert.Null(filter.Filter);
					}
				}

				// Test the serialization, too.
				tip = await Global.RpcClient.GetBestBlockHashAsync();
				var blockHash = tip;
				for (var i = 0; i < indexLines.Length; i++)
				{
					var block = await Global.RpcClient.GetBlockHeaderAsync(blockHash);
					Assert.Contains(blockHash.ToString(), indexLines[indexLines.Length - i - 1]);
					blockHash = block.HashPrevBlock;
				}

				// Assert reorg happened exactly as many times as we reorged.
				Assert.Equal(2, Interlocked.Read(ref _reorgTestAsync_ReorgCount));
			}
			finally
			{
				downloader.Reorged -= ReorgTestAsync_Downloader_Reorged;

				if (downloader != null)
				{
					await downloader.StopAsync();
				}
			}
		}

		private async Task WaitForIndexesToSyncAsync(TimeSpan timeout, IndexDownloader downloader, bool noticesProcessedIndex)
		{
			_filterProcessed = 0;
			var hadIt = true;

			var bestHash = await Global.RpcClient.GetBestBlockHashAsync();

			var times = 0;
			while (downloader.GetFiltersIncluding(new Height(0)).SingleOrDefault(x => x.BlockHash == bestHash) == null)
			{
				hadIt = false;
				if (times > timeout.TotalSeconds)
				{
					throw new TimeoutException($"{nameof(IndexDownloader)} test timed out. Filter wasn't downloaded.");
				}
				await Task.Delay(TimeSpan.FromSeconds(1));
				times++;
			}

			if(hadIt && noticesProcessedIndex)
			{
				times = 0;
				while (Interlocked.Read(ref _filterProcessed) != 0)
				{
					hadIt = false;
					if (times > 7)
					{
						throw new TimeoutException($"{nameof(IndexDownloader)} test timed out. Filter wasn't processed.");
					}
					await Task.Delay(TimeSpan.FromSeconds(1));
					times++;
				}

			}
		}

		private long _reorgTestAsync_ReorgCount;
		private void ReorgTestAsync_Downloader_Reorged(object sender, uint256 e)
		{
			Assert.NotNull(e);
			Interlocked.Increment(ref _reorgTestAsync_ReorgCount);
		}

		#endregion

		#region ClientTests

		[Fact]
		public async Task WalletTestsAsync()
		{
			// Make sure fitlers are created on the server side.
			await AssertFiltersInitializedAsync();

			var network = Global.RpcClient.Network;

			// Create the services.
			// 1. Create connection service.
			var nodes = new NodesGroup(Global.Config.Network,
					requirements: new NodeRequirement
					{
						RequiredServices = NodeServices.Network,
						MinVersion = ProtocolVersion.WITNESS_VERSION
					});
			nodes.ConnectedNodes.Add(Fixture.BackendRegTestNode.CreateNodeClient());

			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			memPoolService.TransactionReceived += WalletTestsAsync_MemPoolService_TransactionReceived;
			Node node = Fixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			// 3. Create index downloader service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(WalletTestsAsync), $"Index{Global.RpcClient.Network}.dat");
			var indexDownloader = new IndexDownloader(Global.RpcClient.Network, indexFilePath, new Uri(Fixture.BackendEndPoint));

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out Mnemonic mnemonic, "password");

			// 5. Create wallet service.
			var blocksFolderPath = Path.Combine(SharedFixture.DataDir, nameof(WalletTestsAsync), $"Blocks");
			var wallet = new WalletService(keyManager, indexDownloader, memPoolService, nodes, blocksFolderPath);
			wallet.NewFilterProcessed += IncrementFilterProcessed;

			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("foo label");
			var txid = await Global.RpcClient.SendToAddressAsync(key.GetP2wpkhAddress(network), new Money(0.1m, MoneyUnit.BTC));
			await Global.RpcClient.GenerateAsync(1);

			try
			{
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				indexDownloader.Synchronize(requestInterval: TimeSpan.FromSeconds(3)); // Start index downloader service.

				// Wait until the filter our previous transaction is present.
				await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(30), indexDownloader, true);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}
				Assert.Equal(1, await wallet.CountBlocksAsync());

				Assert.Single(wallet.Coins);
				var firstCoin = wallet.Coins.Single();
				Assert.Equal(new Money(0.1m, MoneyUnit.BTC), firstCoin.Amount);
				Assert.Equal(indexDownloader.GetBestFilter().BlockHeight, firstCoin.Height);
				Assert.InRange(firstCoin.Index, 0, 1);
				Assert.True(firstCoin.Unspent);
				Assert.Equal("foo label", firstCoin.Label);
				Assert.Equal(key.GetP2wpkhScript(), firstCoin.ScriptPubKey);
				Assert.Null(firstCoin.SpenderTransactionId);
				Assert.NotNull(firstCoin.SpentOutputs);
				Assert.NotEmpty(firstCoin.SpentOutputs);
				Assert.Equal(txid, firstCoin.TransactionId);
				Assert.Single(keyManager.GetKeys(KeyState.Used, false));
				Assert.Equal("foo label", keyManager.GetKeys(KeyState.Used, false).Single().Label);

				// Get some money, make it confirm.
				var key2 = wallet.GetReceiveKey("bar label");
				var txid2 = await Global.RpcClient.SendToAddressAsync(key2.GetP2wpkhAddress(network), new Money(0.01m, MoneyUnit.BTC));
				await Global.RpcClient.GenerateAsync(1);
				var txid3 = await Global.RpcClient.SendToAddressAsync(key2.GetP2wpkhAddress(network), new Money(0.02m, MoneyUnit.BTC));
				await Global.RpcClient.GenerateAsync(1);

				await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(30), indexDownloader, true);
				Assert.Equal(3, await wallet.CountBlocksAsync());

				Assert.Equal(3, wallet.Coins.Count);
				firstCoin = wallet.Coins.OrderBy(x => x.Height).First();
				var secondCoin = wallet.Coins.OrderBy(x => x.Height).Take(2).Last();
				var thirdCoin = wallet.Coins.OrderBy(x => x.Height).Last();
				Assert.Equal(new Money(0.01m, MoneyUnit.BTC), secondCoin.Amount);
				Assert.Equal(new Money(0.02m, MoneyUnit.BTC), thirdCoin.Amount);
				Assert.Equal(indexDownloader.GetBestFilter().BlockHeight.Value - 2, firstCoin.Height.Value);
				Assert.Equal(indexDownloader.GetBestFilter().BlockHeight.Value - 1, secondCoin.Height.Value);
				Assert.Equal(indexDownloader.GetBestFilter().BlockHeight, thirdCoin.Height);
				Assert.True(thirdCoin.Unspent);
				Assert.Equal("foo label", firstCoin.Label);
				Assert.Equal("bar label", secondCoin.Label);
				Assert.Equal("bar label", thirdCoin.Label);
				Assert.Equal(key.GetP2wpkhScript(), firstCoin.ScriptPubKey);
				Assert.Equal(key2.GetP2wpkhScript(), secondCoin.ScriptPubKey);
				Assert.Equal(key2.GetP2wpkhScript(), thirdCoin.ScriptPubKey);
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
				Assert.Empty(keyManager.GetKeys(KeyState.Locked, true));
				Assert.Empty(keyManager.GetKeys(KeyState.Locked));
				Assert.Equal(21, keyManager.GetKeys(KeyState.Clean, true).Count());
				Assert.Equal(21, keyManager.GetKeys(KeyState.Clean, false).Count());
				Assert.Equal(42, keyManager.GetKeys(KeyState.Clean).Count());
				Assert.Equal(44, keyManager.GetKeys().Count());

				Assert.Single(keyManager.GetKeys(KeyState.Used, false).Where(x => x.Label == "foo label"));
				Assert.Single(keyManager.GetKeys(KeyState.Used, false).Where(x => x.Label == "bar label"));

				// REORG TESTS
				await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(90), indexDownloader, true);
				var tx4Res = await Global.RpcClient.SendCommandAsync(RPCOperations.sendtoaddress, key2.GetP2wpkhAddress(network).ToString(), new Money(0.03m, MoneyUnit.BTC).ToString(false, true), "", "", false, true);
				var txid4 = new uint256(tx4Res.ResultString);
				await Global.RpcClient.GenerateAsync(2);
				await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(90), indexDownloader, true);
				Assert.NotEmpty(wallet.Coins.Where(x => x.TransactionId == txid4));
				var tip = await Global.RpcClient.GetBestBlockHashAsync();
				await Global.RpcClient.InvalidateBlockAsync(tip); // Reorg 1
				tip = await Global.RpcClient.GetBestBlockHashAsync();
				await Global.RpcClient.InvalidateBlockAsync(tip); // Reorg 2
				var tx4bumpRes = await Global.RpcClient.SendCommandAsync("bumpfee", txid4.ToString()); // RBF it
				var tx4bump = new uint256(tx4bumpRes.Result["txid"].ToString());
				await Global.RpcClient.GenerateAsync(3);
				await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(90), indexDownloader, true);

				Assert.Equal(4, await wallet.CountBlocksAsync());

				Assert.Equal(4, wallet.Coins.Count);
				Assert.Empty(wallet.Coins.Where(x => x.TransactionId == txid4));
				Assert.NotEmpty(wallet.Coins.Where(x => x.TransactionId == tx4bump));
				var rbfCoin = wallet.Coins.Where(x => x.TransactionId == tx4bump).Single();

				Assert.Equal(new Money(0.03m, MoneyUnit.BTC), rbfCoin.Amount);
				Assert.Equal(indexDownloader.GetBestFilter().BlockHeight.Value - 2, rbfCoin.Height.Value);
				Assert.True(rbfCoin.Unspent);
				Assert.Equal("bar label", rbfCoin.Label);
				Assert.Equal(key2.GetP2wpkhScript(), rbfCoin.ScriptPubKey);
				Assert.Null(rbfCoin.SpenderTransactionId);
				Assert.NotNull(rbfCoin.SpentOutputs);
				Assert.NotEmpty(rbfCoin.SpentOutputs);
				Assert.Equal(tx4bump, rbfCoin.TransactionId);

				Assert.Equal(2, keyManager.GetKeys(KeyState.Used, false).Count());
				Assert.Empty(keyManager.GetKeys(KeyState.Used, true));
				Assert.Equal(2, keyManager.GetKeys(KeyState.Used).Count());
				Assert.Empty(keyManager.GetKeys(KeyState.Locked, false));
				Assert.Empty(keyManager.GetKeys(KeyState.Locked, true));
				Assert.Empty(keyManager.GetKeys(KeyState.Locked));
				Assert.Equal(21, keyManager.GetKeys(KeyState.Clean, true).Count());
				Assert.Equal(21, keyManager.GetKeys(KeyState.Clean, false).Count());
				Assert.Equal(42, keyManager.GetKeys(KeyState.Clean).Count());
				Assert.Equal(44, keyManager.GetKeys().Count());

				Assert.Single(keyManager.GetKeys(KeyState.Used, false).Where(x => x.Label == "foo label"));
				Assert.Single(keyManager.GetKeys(KeyState.Used, false).Where(x => x.Label == "bar label"));

				// TEST MEMPOOL
				var txid5 = await Global.RpcClient.SendToAddressAsync(key.GetP2wpkhAddress(network), new Money(0.1m, MoneyUnit.BTC));
				await Task.Delay(1000); // Wait tx to arrive and get processed.
				Assert.NotEmpty(wallet.Coins.Where(x => x.TransactionId == txid5));
				var mempoolCoin = wallet.Coins.Where(x => x.TransactionId == txid5).Single();
				Assert.Equal(Height.MemPool, mempoolCoin.Height);

				await Global.RpcClient.GenerateAsync(1);
				await WaitForIndexesToSyncAsync(TimeSpan.FromMinutes(1), indexDownloader, true);
				Assert.Equal(indexDownloader.GetBestFilter().BlockHeight, mempoolCoin.Height);
			}
			finally
			{
				wallet.NewFilterProcessed -= IncrementFilterProcessed;
				wallet?.Dispose();
				// Dispose index downloader service.
				if (indexDownloader != null)
				{
					await indexDownloader.StopAsync();
				}

				// Dispose mempool service.
				memPoolService.TransactionReceived -= WalletTestsAsync_MemPoolService_TransactionReceived;

				// Dispose connection service.
				nodes?.Dispose();
			}
		}

		private long _filterProcessed;
		private void IncrementFilterProcessed(object sender, FilterModel e)
		{
			Interlocked.Increment(ref _filterProcessed);
		}

		private void WalletTestsAsync_MemPoolService_TransactionReceived(object sender, SmartTransaction e)
		{

		}

		[Fact]
		public async Task SendingTestsAsync()
		{
			// Make sure fitlers are created on the server side.
			await AssertFiltersInitializedAsync();

			var network = Global.RpcClient.Network;

			// Create the services.
			// 1. Create connection service.
			var nodes = new NodesGroup(Global.Config.Network,
					requirements: new NodeRequirement
					{
						RequiredServices = NodeServices.Network,
						MinVersion = ProtocolVersion.WITNESS_VERSION
					});
			nodes.ConnectedNodes.Add(Fixture.BackendRegTestNode.CreateNodeClient());

			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			Node node = Fixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			// 3. Create index downloader service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(SendingTestsAsync), $"Index{Global.RpcClient.Network}.dat");
			var indexDownloader = new IndexDownloader(Global.RpcClient.Network, indexFilePath, new Uri(Fixture.BackendEndPoint));

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out Mnemonic mnemonic, "password");

			// 5. Create wallet service.
			var blocksFolderPath = Path.Combine(SharedFixture.DataDir, nameof(SendingTestsAsync), $"Blocks");
			var wallet = new WalletService(keyManager, indexDownloader, memPoolService, nodes, blocksFolderPath);
			wallet.NewFilterProcessed += IncrementFilterProcessed;

			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("foo label");
			var txid = await Global.RpcClient.SendToAddressAsync(key.GetP2wpkhAddress(network), new Money(0.1m, MoneyUnit.BTC));
			await Global.RpcClient.GenerateAsync(1);

			try
			{
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				indexDownloader.Synchronize(requestInterval: TimeSpan.FromSeconds(3)); // Start index downloader service.

				// Wait until the filter our previous transaction is present.
				await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(30), indexDownloader, true);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}

				await wallet.BuildTransactionAsync("", new [] { (new Script(), Money.Zero) }, 3, false);
			}
			finally
			{
				wallet.NewFilterProcessed -= IncrementFilterProcessed;
				wallet?.Dispose();
				// Dispose index downloader service.
				if (indexDownloader != null)
				{
					await indexDownloader.StopAsync();
				}

				// Dispose connection service.
				nodes?.Dispose();
			}
		}

		#endregion
	}
}
