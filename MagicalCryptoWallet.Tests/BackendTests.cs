using MagicalCryptoWallet.Backend;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Tests.NodeBuilding;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
	public class BackendTests : IClassFixture<SharedFixture>
	{
		private SharedFixture Fixture { get; }

		private CoreNode SharedRegTestNode { get; }

		public BackendTests(SharedFixture fixture)
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
					var content = await request.Content.ReadAsStringAsync();
					var filterCount = content.Split(',').Count();
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

				var exchangeRates = await res.ReadAsAsync<List<ExchangeRate>>();
				Assert.Single(exchangeRates);

				var rate = exchangeRates[0];
				Assert.Equal("USD", rate.Ticker);
				Assert.True(rate.Rate > 0);
			}
		}

		[Fact]
		public async void BroadcastWithOutMinFeeAsync()
		{
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
	}

	static class HttpResponseMessageExtensions
	{
		public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage me)
		{
			var jsonString = await me.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<T>(jsonString);
		}
	}
}
