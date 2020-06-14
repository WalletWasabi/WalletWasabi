using NBitcoin;
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend;
using WalletWasabi.Backend.Controllers;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.CoinJoin.Client.Rounds;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Crypto;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.TorSocks5;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests
{
	[Collection("RegTest collection")]
	public class BackendTests
	{
#pragma warning disable IDE0059 // Value assigned to symbol is never used

		public BackendTests(RegTestFixture regTestFixture)
		{
			RegTestFixture = regTestFixture;
		}

		private RegTestFixture RegTestFixture { get; }

		#region BackendTests

		[Fact]
		public async Task GetExchangeRatesAsync()
		{
			using var client = new TorHttpClient(new Uri(RegTestFixture.BackendEndPoint), null);
			using var response = await client.SendAsync(HttpMethod.Get, $"/api/v{Constants.BackendMajorVersion}/btc/offchain/exchange-rates");
			Assert.True(response.StatusCode == HttpStatusCode.OK);

			var exchangeRates = await response.Content.ReadAsJsonAsync<List<ExchangeRate>>();
			Assert.Single(exchangeRates);

			var rate = exchangeRates[0];
			Assert.Equal("USD", rate.Ticker);
			Assert.True(rate.Rate > 0);
		}

		[Fact]
		public async Task GetClientVersionAsync()
		{
			using var client = new WasabiClient(new Uri(RegTestFixture.BackendEndPoint), null);
			var uptodate = await client.CheckUpdatesAsync(CancellationToken.None);
			Assert.True(uptodate.BackendCompatible);
			Assert.True(uptodate.ClientUpToDate);
		}

		[Fact]
		public async Task BroadcastReplayTxAsync()
		{
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

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
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

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
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			var indexBuilderServiceDir = Common.GetWorkDir();
			var indexFilePath = Path.Combine(indexBuilderServiceDir, $"Index{rpc.Network}.dat");

			var indexBuilderService = new IndexBuilderService(rpc, global.HostedServices.FirstOrDefault<BlockNotifier>(), indexFilePath);
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
					Assert.Equal(expectedHash, filterModel.Header.BlockHash);
				}
			}
			finally
			{
				if (indexBuilderService is { })
				{
					await indexBuilderService.StopAsync();
				}
			}
		}

		[Fact]
		public async Task StatusRequestTestAsync()
		{
			const string Request = "/api/v3/btc/Blockchain/status";
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			var indexBuilderService = global.IndexBuilderService;
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

				using var client = new WasabiClient(new Uri(RegTestFixture.BackendEndPoint), null);
				var response = await client.TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, Request);
				using (HttpContent content = response.Content)
				{
					var resp = await content.ReadAsJsonAsync<StatusResponse>();
					Assert.True(resp.FilterCreationActive);
				}

				// Simulate an unintended stop
				await indexBuilderService.StopAsync();
				indexBuilderService = null;

				await rpc.GenerateAsync(1);

				response = await client.TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, Request);
				using (HttpContent content = response.Content)
				{
					var resp = await content.ReadAsJsonAsync<StatusResponse>();
					Assert.True(resp.FilterCreationActive);
				}

				await rpc.GenerateAsync(1);

				var blockchainController = (BlockchainController)RegTestFixture.BackendHost.Services.GetService(typeof(BlockchainController));
				blockchainController.Cache.Remove($"{nameof(BlockchainController.GetStatusAsync)}");

				// Set back the time to trigger timeout in BlockchainController.GetStatusAsync.
				global.IndexBuilderService.LastFilterBuildTime = DateTimeOffset.UtcNow - BlockchainController.FilterTimeout;

				response = await client.TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, Request);
				using (HttpContent content = response.Content)
				{
					var resp = await content.ReadAsJsonAsync<StatusResponse>();
					Assert.False(resp.FilterCreationActive);
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

		#endregion BackendTests

#pragma warning restore IDE0059 // Value assigned to symbol is never used
	}
}
