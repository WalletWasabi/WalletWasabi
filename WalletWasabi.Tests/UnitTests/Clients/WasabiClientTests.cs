using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.TorSocks5;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	public class WasabiClientTests
	{
		[Fact]
		public async Task GetTransactionsTest()
		{
			var mempool = Enumerable.Range(0, 1_100).Select(_=> CreateTransaction()).ToArray();

			Task<HttpResponseMessage> FakeServerCode(HttpMethod method, string action, string[] parameters)
			{
				Assert.True(parameters.Length <= 10);
				var requestedTxId = parameters.Select(p => uint256.Parse(p[(p.IndexOf('=')+1)..]));
				var result = mempool.Where(tx => requestedTxId.Contains(tx.GetHash())).Select(tx=> tx.ToHex());

				var response = new HttpResponseMessage(HttpStatusCode.OK);
				response.Content = new StringContent(JsonConvert.SerializeObject(result));
				return Task.FromResult(response);
			};

			var torHttpClient = new MockTorHttpClient();
			torHttpClient.OnSendAsync_Method = FakeServerCode;
			var client = new WasabiClient(torHttpClient);
			Assert.Empty(WasabiClient.TransactionCache);

			// Requests one transaction
			var searchedTxId = mempool[0].GetHash();
			var txs = await client.GetTransactionsAsync(Network.Main, new[]{ searchedTxId }, CancellationToken.None);

			Assert.Equal(searchedTxId, txs.First().GetHash());
			Assert.NotEmpty(WasabiClient.TransactionCache);
			Assert.True(WasabiClient.TransactionCache.ContainsKey(searchedTxId));

			// Requests 20 transaction
			var searchedTxIds = mempool[..20].Select(x=>x.GetHash());
			txs = await client.GetTransactionsAsync(Network.Main, searchedTxIds, CancellationToken.None);
			Assert.Equal(20, txs.Count());

			// Requests 1100 transaction 
			searchedTxIds = mempool.Select(x=>x.GetHash());
			txs = await client.GetTransactionsAsync(Network.Main, searchedTxIds, CancellationToken.None);
			Assert.Equal(1_100, txs.Count());
			Assert.Equal(1_000, WasabiClient.TransactionCache.Count());

			Assert.Subset(WasabiClient.TransactionCache.Keys.ToHashSet(), txs.TakeLast(1_000).Select(x=>x.GetHash()).ToHashSet());

			// Requests transactions that are already in the cache
			torHttpClient.OnSendAsync_Method = (verb, action, parameters)=> 
				Task.FromException<HttpResponseMessage>(
					new InvalidOperationException("The transaction should already be in the client cache. Http request was unexpected."));

			var expectedTobeCachedTxId = mempool.Last().GetHash();
			txs = await client.GetTransactionsAsync(Network.Main, new[]{ expectedTobeCachedTxId }, CancellationToken.None);
			Assert.Equal(expectedTobeCachedTxId, txs.Last().GetHash());

			// Requests fails with Bad Request
			torHttpClient.OnSendAsync_Method = (verb, action, parameters)=> {
				var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
				response.Content = new StringContent("\"Some RPC problem...\"");
				return Task.FromResult(response);
			};

			var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
				await client.GetTransactionsAsync(Network.Main, new[]{ RandomUtils.GetUInt256() }, CancellationToken.None));
			Assert.Equal("Bad Request\nSome RPC problem...", ex.Message);
		}

		private static Transaction CreateTransaction()
		{
			var coins = Enumerable.Range(0, 10).Select(_ => new Coin(RandomUtils.GetUInt256(), 0u, Money.Coins(10), Script.Empty)).ToArray();
			var tx = Network.RegTest.CreateTransaction();
			foreach (var coin in coins)
			{
				tx.Inputs.Add(coin.Outpoint, Script.Empty, WitScript.Empty);
			}
			tx.Outputs.Add(Money.Coins(3), Script.Empty);
			tx.Outputs.Add(Money.Coins(2), Script.Empty);
			tx.Outputs.Add(Money.Coins(1), Script.Empty);
			return tx;
		}
	}

	class MockTorHttpClient : ITorHttpClient
	{
		public Uri DestinationUri => new Uri("DestinationUri");

		public Func<Uri> DestinationUriAction => () => DestinationUri;

		public EndPoint TorSocks5EndPoint => IPEndPoint.Parse("127.0.0.1:9050");

		public bool IsTorUsed => true;

		public Func<HttpMethod, string, string[], Task<HttpResponseMessage>> OnSendAsync_Method { get; set; }

		public Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent content = null, CancellationToken cancel = default)
		{
			var sepPos = relativeUri.IndexOf('?');
			var action = relativeUri[..sepPos];
			var parameters = relativeUri[(sepPos+1)..].Split('&', StringSplitOptions.RemoveEmptyEntries);
			return OnSendAsync_Method(method, action, parameters);
		}

		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel = default)
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
		}
	}
}
