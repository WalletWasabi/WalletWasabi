using MagicalCryptoWallet.Backend;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Exceptions;
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
				using (var client = new TorHttpClient(new Uri(RegTestFixture.BackendEndPoint)))
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
			using (var client = new TorHttpClient(new Uri(RegTestFixture.BackendEndPoint)))
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
			using (var client = new TorHttpClient(new Uri(RegTestFixture.BackendEndPoint)))
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
			using (var client = new TorHttpClient(new Uri(RegTestFixture.BackendEndPoint)))
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
			using (var client = new TorHttpClient(new Uri(RegTestFixture.BackendEndPoint)))
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

			var downloader = new IndexDownloader(Global.RpcClient.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint));
			try
			{
				downloader.Synchronize(requestInterval: TimeSpan.FromSeconds(1));

				// Test initial synchronization.

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

				// Test later synchronization.
				RegTestFixture.BackendRegTestNode.Generate(10);
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

			var node = RegTestFixture.BackendRegTestNode;
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(ReorgTestAsync), $"Index{Global.RpcClient.Network}.dat");

			var downloader = new IndexDownloader(Global.RpcClient.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint));
			try
			{
				downloader.Synchronize(requestInterval: TimeSpan.FromSeconds(3));

				downloader.Reorged += ReorgTestAsync_Downloader_Reorged;

				// Test initial synchronization.	
				await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(90), downloader);

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

				// Test synchronization after fork.
				await Global.RpcClient.InvalidateBlockAsync(tip); // Reorg 1
				tip = await Global.RpcClient.GetBestBlockHashAsync();
				await Global.RpcClient.InvalidateBlockAsync(tip); // Reorg 2
				var tx1bumpRes = await Global.RpcClient.SendCommandAsync("bumpfee", tx1.ToString()); // RBF it
				var tx1bump = new uint256(tx1bumpRes.Result["txid"].ToString());

				await Global.RpcClient.GenerateAsync(5);
				await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(90), downloader);

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

		private async Task WaitForIndexesToSyncAsync(TimeSpan timeout, IndexDownloader downloader)
		{
			var bestHash = await Global.RpcClient.GetBestBlockHashAsync();

			var times = 0;
			while (downloader.GetFiltersIncluding(new Height(0)).SingleOrDefault(x => x.BlockHash == bestHash) == null)
			{
				if (times > timeout.TotalSeconds)
				{
					throw new TimeoutException($"{nameof(IndexDownloader)} test timed out. Filter wasn't downloaded.");
				}
				await Task.Delay(TimeSpan.FromSeconds(1));
				times++;
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
			nodes.ConnectedNodes.Add(RegTestFixture.BackendRegTestNode.CreateNodeClient());

			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			memPoolService.TransactionReceived += WalletTestsAsync_MemPoolService_TransactionReceived;
			Node node = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			// 3. Create index downloader service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(WalletTestsAsync), $"Index{Global.RpcClient.Network}.dat");
			var indexDownloader = new IndexDownloader(Global.RpcClient.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint));

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out Mnemonic mnemonic, "password");

			// 5. Create wallet service.
			var blocksFolderPath = Path.Combine(SharedFixture.DataDir, nameof(WalletTestsAsync), $"Blocks");
			var wallet = new WalletService(keyManager, indexDownloader, memPoolService, nodes, blocksFolderPath);
			wallet.NewFilterProcessed += Wallet_NewFilterProcessed;

			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("foo label");
			var txid = await Global.RpcClient.SendToAddressAsync(key.GetP2wpkhAddress(network), new Money(0.1m, MoneyUnit.BTC));
			await Global.RpcClient.GenerateAsync(1);

			try
			{
				_filtersProcessedByWalletCount = 0;
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				indexDownloader.Synchronize(requestInterval: TimeSpan.FromSeconds(3)); // Start index downloader service.

				// Wait until the filter our previous transaction is present.
				var blockCount = await Global.RpcClient.GetBlockCountAsync();
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

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
				_filtersProcessedByWalletCount = 0;
				await Global.RpcClient.GenerateAsync(1);
				var txid3 = await Global.RpcClient.SendToAddressAsync(key2.GetP2wpkhAddress(network), new Money(0.02m, MoneyUnit.BTC));
				await Global.RpcClient.GenerateAsync(1);

				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 2);
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
				var tx4Res = await Global.RpcClient.SendCommandAsync(RPCOperations.sendtoaddress, key2.GetP2wpkhAddress(network).ToString(), new Money(0.03m, MoneyUnit.BTC).ToString(false, true), "", "", false, true);
				var txid4 = new uint256(tx4Res.ResultString);
				_filtersProcessedByWalletCount = 0;
				await Global.RpcClient.GenerateAsync(2);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 2);
				Assert.NotEmpty(wallet.Coins.Where(x => x.TransactionId == txid4));
				var tip = await Global.RpcClient.GetBestBlockHashAsync();
				await Global.RpcClient.InvalidateBlockAsync(tip); // Reorg 1
				tip = await Global.RpcClient.GetBestBlockHashAsync();
				await Global.RpcClient.InvalidateBlockAsync(tip); // Reorg 2
				var tx4bumpRes = await Global.RpcClient.SendCommandAsync("bumpfee", txid4.ToString()); // RBF it
				var tx4bump = new uint256(tx4bumpRes.Result["txid"].ToString());
				_filtersProcessedByWalletCount = 0;
				await Global.RpcClient.GenerateAsync(3);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 3);

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

				_filtersProcessedByWalletCount = 0;
				await Global.RpcClient.GenerateAsync(1);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);
				var res = await Global.RpcClient.GetTxOutAsync(mempoolCoin.TransactionId, mempoolCoin.Index, true);
				Assert.Equal(indexDownloader.GetBestFilter().BlockHeight, mempoolCoin.Height);
			}
			finally
			{
				wallet.NewFilterProcessed -= Wallet_NewFilterProcessed;
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

		private async Task WaitForFiltersToBeProcessedAsync(TimeSpan timeout, int numberOfFiltersToWaitFor)
		{
			var times = 0;
			while (Interlocked.Read(ref _filtersProcessedByWalletCount) < numberOfFiltersToWaitFor)
			{
				if (times > timeout.TotalSeconds)
				{
					throw new TimeoutException($"{nameof(WalletService)} test timed out. Filter wasn't processed. Needed: {numberOfFiltersToWaitFor}, got only: {_filtersProcessedByWalletCount}.");
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
			nodes.ConnectedNodes.Add(RegTestFixture.BackendRegTestNode.CreateNodeClient());

			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			Node node = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			// 3. Create index downloader service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync), $"Index{Global.RpcClient.Network}.dat");
			var indexDownloader = new IndexDownloader(Global.RpcClient.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint));

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out Mnemonic mnemonic, "password");

			// 5. Create wallet service.
			var blocksFolderPath = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync), $"Blocks");
			var wallet = new WalletService(keyManager, indexDownloader, memPoolService, nodes, blocksFolderPath);
			wallet.NewFilterProcessed += Wallet_NewFilterProcessed;

			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("foo label");
			var txid = await Global.RpcClient.SendToAddressAsync(key.GetP2wpkhAddress(network), new Money(1m, MoneyUnit.BTC));
			await Global.RpcClient.GenerateAsync(2);

			try
			{
				_filtersProcessedByWalletCount = 0;
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				indexDownloader.Synchronize(requestInterval: TimeSpan.FromSeconds(3)); // Start index downloader service.

				// Wait until the filter our previous transaction is present.
				var blockCount = await Global.RpcClient.GetBlockCountAsync();
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}

				var scp = new Key().ScriptPubKey;
				var res2 = await wallet.BuildTransactionAsync("password", new[] { new WalletService.Operation(scp, new Money(0.05m, MoneyUnit.BTC), "foo") }, 5, false);

				Assert.NotNull(res2.Transaction);
				Assert.Single(res2.OuterWalletOutputs);
				Assert.Equal(scp, res2.OuterWalletOutputs.Single().ScriptPubKey);
				Assert.Single(res2.InnerWalletOutputs);
				Assert.True(res2.Fee > new Money(5 * 100)); // since there is a sanity check of 5sat/b in the server
				Assert.InRange(res2.FeePercentOfSent, 0, 1);
				Assert.Single(res2.SpentCoins);
				Assert.Equal(key.GetP2wpkhScript(), res2.SpentCoins.Single().ScriptPubKey);
				Assert.Equal(new Money(1m, MoneyUnit.BTC), res2.SpentCoins.Single().Amount);
				Assert.False(res2.SpendsUnconfirmed);

				await wallet.SendTransactionAsync(res2.Transaction);

				Assert.Contains(res2.InnerWalletOutputs.Single(), wallet.Coins);

				#region Basic

				Script receive = wallet.GetReceiveKey("Basic").GetP2wpkhScript();
				Money amountToSend = wallet.Coins.Where(x => x.Unspent).Sum(x => x.Amount) / 2;
				var res = await wallet.BuildTransactionAsync("password", new[] { new WalletService.Operation(receive, amountToSend, "foo") }, 1008, allowUnconfirmed: true);

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

				#endregion

				#region SubtractFeeFromAmount
				
				receive = wallet.GetReceiveKey("SubtractFeeFromAmount").GetP2wpkhScript();
				amountToSend = wallet.Coins.Where(x => x.Unspent).Sum(x => x.Amount) / 2;
				res = await wallet.BuildTransactionAsync("password", new[] { new WalletService.Operation(receive, amountToSend, "foo") }, 1008, allowUnconfirmed: true, subtractFeeFromAmountIndex: 0);

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

				#endregion

				#region LowFee

				res = await wallet.BuildTransactionAsync("password", new[] { new WalletService.Operation(receive, amountToSend, "foo") }, 1008, allowUnconfirmed: true);

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

				#endregion

				#region MediumFee

				res = await wallet.BuildTransactionAsync("password", new[] { new WalletService.Operation(receive, amountToSend, "foo") }, 144, allowUnconfirmed: true);

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

				#endregion

				#region HighFee

				res = await wallet.BuildTransactionAsync("password", new[] { new WalletService.Operation(receive, amountToSend, "foo") }, 2, allowUnconfirmed: true);

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

				#endregion

				#region MaxAmount

				receive = wallet.GetReceiveKey("MaxAmount").GetP2wpkhScript();
				res = await wallet.BuildTransactionAsync("password", new[] { new WalletService.Operation(receive, Money.Zero, "foo") }, 1008, allowUnconfirmed: true);

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
				Assert.Equal(wallet.Coins.Where(x => x.Unspent).Sum(x => x.Amount) - res.Fee, maxBuiltTxOutput.Value);

				await wallet.SendTransactionAsync(res.Transaction);

				#endregion

				#region InputSelection

				receive = wallet.GetReceiveKey("InputSelection").GetP2wpkhScript();

				var inputCountBefore = res.SpentCoins.Count();
				res = await wallet.BuildTransactionAsync("password", new[] { new WalletService.Operation(receive, Money.Zero, "foo") }, 1008,
					allowUnconfirmed: true,
					allowedInputs: wallet.Coins.Where(x=>x.Unspent).Select(x=>new TxoRef(x.TransactionId, x.Index)).Take(1));

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

				res = await wallet.BuildTransactionAsync("password", new[] { new WalletService.Operation(receive, Money.Zero, "foo") }, 1008, 
					allowUnconfirmed: true,
					allowedInputs: new[] { res.SpentCoins.Select(x => new TxoRef(x.TransactionId, x.Index)).First() });

				Assert.Single(res.InnerWalletOutputs);
				Assert.Empty(res.OuterWalletOutputs);
				activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);

				Assert.Single(res.Transaction.Transaction.Inputs);
				Assert.Single(res.Transaction.Transaction.Outputs);
				Assert.Single(res.SpentCoins);

				#endregion

				#region Labeling

				res = await wallet.BuildTransactionAsync("password", new[] { new WalletService.Operation(receive, Money.Zero, "my label") }, 1008,
					allowUnconfirmed: true);

				Assert.Single(res.InnerWalletOutputs);
				Assert.Equal("change of (my label)", res.InnerWalletOutputs.Single().Label);

				amountToSend = wallet.Coins.Where(x => x.Unspent).Sum(x => x.Amount) / 3;
				res = await wallet.BuildTransactionAsync("password", new[] {
					new WalletService.Operation(new Key().ScriptPubKey, amountToSend, "outgoing"),
					new WalletService.Operation(new Key().ScriptPubKey, amountToSend, "outgoing2")
				}, 1008,
					allowUnconfirmed: true);

				Assert.Single(res.InnerWalletOutputs);
				Assert.Equal(2, res.OuterWalletOutputs.Count());
				Assert.Equal("change of (outgoing, outgoing2)", res.InnerWalletOutputs.Single().Label);

				await wallet.SendTransactionAsync(res.Transaction);

				Assert.Contains("change of (outgoing, outgoing2)", wallet.Coins.Where(x=>x.Height == Height.MemPool).Select(x => x.Label));
				Assert.Contains("change of (outgoing, outgoing2)", keyManager.GetKeys().Select(x => x.Label));

				_filtersProcessedByWalletCount = 0;
				await Global.RpcClient.GenerateAsync(1);
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);

				var bestHeight = wallet.IndexDownloader.GetBestFilter().BlockHeight;
				Assert.Contains("change of (outgoing, outgoing2)", wallet.Coins.Where(x => x.Height == bestHeight).Select(x => x.Label));
				Assert.Contains("change of (outgoing, outgoing2)", keyManager.GetKeys().Select(x => x.Label));

				#endregion
			}
			finally
			{
				wallet.NewFilterProcessed -= Wallet_NewFilterProcessed;
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

		[Fact]
		public async Task BuildTransactionValidationsTest()
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
			nodes.ConnectedNodes.Add(RegTestFixture.BackendRegTestNode.CreateNodeClient());
			
			// 2. Create mempool service.
			var memPoolService = new MemPoolService();
			Node node = RegTestFixture.BackendRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));

			// 3. Create index downloader service.
			var indexFilePath = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync), $"Index{Global.RpcClient.Network}.dat");
			var indexDownloader = new IndexDownloader(Global.RpcClient.Network, indexFilePath, new Uri(RegTestFixture.BackendEndPoint));

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out Mnemonic mnemonic, "password");
			
			// 5. Create wallet service.
			var blocksFolderPath = Path.Combine(SharedFixture.DataDir, nameof(SendTestsFromHiddenWalletAsync), $"Blocks");
			var wallet = new WalletService(keyManager, indexDownloader, memPoolService, nodes, blocksFolderPath);
			wallet.NewFilterProcessed += Wallet_NewFilterProcessed;

			var scp = new Key().ScriptPubKey;
			var validOperationList = new[]{ new WalletService.Operation(scp, Money.Coins(1)) };
			var invalidOperationList = new[]{ new WalletService.Operation(scp, Money.Coins(10 * 1000 * 1000)), new WalletService.Operation(scp, Money.Coins(12 * 1000 * 1000)) };
			var overflowOperationList = new[]{ 
				new WalletService.Operation(scp, Money.Satoshis(long.MaxValue)), 
				new WalletService.Operation(scp, Money.Satoshis(long.MaxValue)),
				new WalletService.Operation(scp, Money.Satoshis(5))
				};

			// toSend cannot be null
			await Assert.ThrowsAsync<ArgumentNullException>( async ()=> await wallet.BuildTransactionAsync(null, null, 0) );

			// toSend cannot have a null element
			await Assert.ThrowsAsync<ArgumentException>( async ()=> await wallet.BuildTransactionAsync(null, new[]{ (WalletService.Operation)null }, 0) );

			// toSend cannot have a zero elements
			await Assert.ThrowsAsync<ArgumentException>( async ()=> await wallet.BuildTransactionAsync(null, new WalletService.Operation[0], 0) );

			// feeTarget has to be in the range 0 to 1008
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>( async ()=> await wallet.BuildTransactionAsync(null, validOperationList, -10) );
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>( async ()=> await wallet.BuildTransactionAsync(null, validOperationList , 2000) );

			// subtractFeeFromAmountIndex has to be valid
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>( async ()=> await wallet.BuildTransactionAsync(null, validOperationList, 2, false, -10) );
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>( async ()=> await wallet.BuildTransactionAsync(null, validOperationList, 2, false, 1) );

			// toSend amount sum has to be in range 0 to 2099999997690000
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>( async ()=> await wallet.BuildTransactionAsync(null, invalidOperationList, 2) );

			// toSend ammount sum has to be less than ulong.MaxValue
			await Assert.ThrowsAsync<OverflowException>( async ()=> await wallet.BuildTransactionAsync(null, overflowOperationList, 2) );

			// allowedInputs cannot be empty
			await Assert.ThrowsAsync<ArgumentException>( async ()=> await wallet.BuildTransactionAsync(null, validOperationList, 2, false, null, null, new TxoRef[0]) );


			// Get some money, make it confirm.
			var key = wallet.GetReceiveKey("foo label");
			var txid = await Global.RpcClient.SendToAddressAsync(key.GetP2wpkhAddress(network), new Money(1m, MoneyUnit.BTC));

			// Generate some coins
			await Global.RpcClient.GenerateAsync(2);

			try
			{
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				indexDownloader.Synchronize(requestInterval: TimeSpan.FromSeconds(3)); // Start index downloader service.

				// Wait until the filter our previous transaction is present.
				var blockCount = await Global.RpcClient.GetBlockCountAsync();
				await WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.InitializeAsync(cts.Token); // Initialize wallet service.
				}

				// No enough money
				var operations = new[]{ new WalletService.Operation(scp, Money.Coins(2m)) };
				await Assert.ThrowsAsync<InsufficientBalanceException>( async ()=> await wallet.BuildTransactionAsync(null, operations, 2) );

				var txid2 = await Global.RpcClient.SendToAddressAsync(key.GetP2wpkhAddress(network), new Money(1m, MoneyUnit.BTC));
				await Assert.ThrowsAsync<InsufficientBalanceException>( async ()=> await wallet.BuildTransactionAsync(null, operations, 2, true) );


				// Only one operation with Zero money
				operations = new[]{ 
					new WalletService.Operation(scp, Money.Zero),
					new WalletService.Operation(scp, Money.Zero) };
				await Assert.ThrowsAsync<ArgumentException>( async ()=> await wallet.BuildTransactionAsync(null, operations, 2) );

				// `Custom change` and `spend all` cannot be specified at the same time
				await Assert.ThrowsAsync<ArgumentException>( async ()=> await wallet.BuildTransactionAsync(null, operations, 2, false, null, Script.Empty) );

				operations = new[]{ new WalletService.Operation(scp, Money.Coins(0.5m)) };
				var btx =  await wallet.BuildTransactionAsync("password", operations, 2);
				
				operations = new[]{ new WalletService.Operation(scp, Money.Coins(0.00005m)) };
				btx =  await wallet.BuildTransactionAsync("password", operations, 2, false, 0);
				Assert.True( btx.FeePercentOfSent > 20 );
				Assert.Equal( 1, btx.SpentCoins.Count() );
				Assert.Equal( txid, btx.SpentCoins.First().TransactionId );
				Assert.Equal( false, btx.Transaction.Transaction.RBF ); // Is this okay



			}
			finally
			{
				wallet?.Dispose();
				// Dispose index downloader service.
				await indexDownloader?.StopAsync();
				// Dispose connection service.
				nodes?.Dispose();
			}
			
		}
		#endregion
	}
}
