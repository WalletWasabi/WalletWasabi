using MagicalCryptoWallet.Backend;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using MagicalCryptoWallet.Services;
using MagicalCryptoWallet.Tests.NodeBuilding;
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

		private static CoreNode SharedRegTestNode { get; set; }

		public RegTests(SharedFixture fixture)
		{
			Fixture = fixture;

			if (Fixture.BackEndNodeBuilder == null)
			{
				Fixture.BackEndNodeBuilder = NodeBuilder.Create();
				Fixture.BackEndNodeBuilder.CreateNode();
				Fixture.BackEndNodeBuilder.StartAll();
				SharedRegTestNode = Fixture.BackEndNodeBuilder.Nodes[0];
				SharedRegTestNode.Generate(101);
				var rpc = SharedRegTestNode.CreateRPCClient();

				var authString = rpc.Authentication.Split(':');
				Global.InitializeAsync(rpc.Network, authString[0], authString[1], rpc).GetAwaiter().GetResult();

				Fixture.BackendEndPoint = $"http://localhost:{new Random().Next(37130, 38000)}/";
				Fixture.BackendHost = WebHost.CreateDefaultBuilder()
					.UseStartup<Startup>()
					.UseUrls(Fixture.BackendEndPoint)
					.Build();
				Fixture.BackendHost.RunAsync();
				Logger.LogInfo<SharedFixture>($"Started Backend webhost: {Fixture.BackendEndPoint}");

				Task.Delay(3000).GetAwaiter().GetResult(); // Wait for server to initialize (Without this OSX CI will fail)
			}
		}

		[Fact]
		public async Task FilterInitializationAsync()
		{
			await AssertFiltersInitializedAsync();
		}

		private async Task AssertFiltersInitializedAsync()
		{
			var firstHash = Global.RpcClient.GetBlockHash(0);
			while (true)
			{
				using (var client = new HttpClient() { BaseAddress = new Uri(Fixture.BackendEndPoint) })
				using (var request = await client.GetAsync("/api/v1/btc/Blockchain/filters/" + firstHash))
				{
					var filters = await request.Content.ReadAsJsonAsync<List<string>>();
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

		[Fact]
		public async void GetExchangeRatesAsyncAsync()
		{
			using (var client = new HttpClient() { BaseAddress = new Uri(Fixture.BackendEndPoint) })
			using (var res = await client.GetAsync("/api/v1/btc/Blockchain/exchange-rates"))
			{
				Assert.True(res.IsSuccessStatusCode);

				var exchangeRates = await res.Content.ReadAsJsonAsync<List<ExchangeRate>>();
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
			using (var client = new HttpClient() { BaseAddress = new Uri(Fixture.BackendEndPoint) })
			using (var res = await client.PostAsync("/api/v1/btc/Blockchain/broadcast", content))
			{

				Assert.False(res.IsSuccessStatusCode);
				Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
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
			using (var client = new HttpClient() { BaseAddress = new Uri(Fixture.BackendEndPoint) })
			using (var res = await client.PostAsync("/api/v1/btc/Blockchain/broadcast", content))
			{
				Assert.True(res.IsSuccessStatusCode);
				Assert.Equal("\"Transaction is already in the blockchain.\"", await res.Content.ReadAsStringAsync());
			}
		}

		[Fact]
		public async void BroadcastInvalidTxAsync()
		{
			var content = new StringContent($"''", Encoding.UTF8, "application/json");
			using (var client = new HttpClient() { BaseAddress = new Uri(Fixture.BackendEndPoint) })
			using (var res = await client.PostAsync("/api/v1/btc/Blockchain/broadcast", content))
			{
				Assert.False(res.IsSuccessStatusCode);
				Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
				Assert.Equal("\"Invalid hex.\"", await res.Content.ReadAsStringAsync());
			}
		}

		[Fact]
		public async Task DownloaderAsync()
		{
			var blocksToDownload = new HashSet<uint256>();
			for (int i = 0; i < 101; i++)
			{
				var hash = await Global.RpcClient.GetBlockHashAsync(i);
				blocksToDownload.Add(hash);
			}
			var blocksFolderPath = Path.Combine(SharedFixture.DataDir, nameof(DownloaderAsync), $"Blocks");
			BlockDownloader downloader = null;
			try
			{
				using (var nodes = new NodesGroup(Global.Config.Network,
					requirements: new NodeRequirement
					{
						RequiredServices = NodeServices.Network,
						MinVersion = ProtocolVersion.WITNESS_VERSION
					}))
				{
					nodes.ConnectedNodes.Add(SharedRegTestNode.CreateNodeClient());

					downloader = new BlockDownloader(nodes, blocksFolderPath);
					downloader.Start();
					foreach (var hash in blocksToDownload)
					{
						downloader.QueToDownload(hash);
					}

					nodes.Connect();

					foreach (var hash in blocksToDownload)
					{
						var times = 0;
						while (downloader.GetBlock(hash) == null)
						{
							if (times > 300) // 30 seconds
							{
								throw new TimeoutException($"{nameof(BlockDownloader)} test timed out.");
							}
							await Task.Delay(100);
							times++;
						}
						Assert.True(File.Exists(Path.Combine(blocksFolderPath, hash.ToString())));
					}
					Logger.LogInfo<P2pTests>($"All RegTest block is downloaded.");
				}
			}
			finally
			{
				downloader?.Stop();

				// So next test will download the block.
				foreach (var hash in blocksToDownload)
				{
					downloader?.TryRemove(hash);
				}
				if (Directory.Exists(blocksFolderPath))
				{
					Directory.Delete(blocksFolderPath, recursive: true);
				}
			}
		}

		[Fact]
		public async Task MempoolAsync()
		{
			var memPoolService = new MemPoolService();
			Node node = SharedRegTestNode.CreateNodeClient();
			node.Behaviors.Add(new MemPoolBehavior(memPoolService));
			node.VersionHandshake();

			try
			{
				memPoolService.TransactionReceived += MemPoolService_TransactionReceived;
				
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
				memPoolService.TransactionReceived -= MemPoolService_TransactionReceived;
			}
		}

		private long _mempoolTransactionCount = 0;
		private void MemPoolService_TransactionReceived(object sender, SmartTransaction e)
		{
			Interlocked.Increment(ref _mempoolTransactionCount);
			Logger.LogDebug<P2pTests>($"Mempool transaction received: {e.GetHash()}.");
		}
	}
}
