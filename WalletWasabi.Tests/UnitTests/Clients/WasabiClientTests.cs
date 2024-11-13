using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients;

public class WasabiClientTests
{
	[Fact]
	public async Task GetTransactionsTestAsync()
	{
		var mempool = Enumerable.Range(0, 1_100).Select(_ => CreateTransaction()).ToArray();

		async Task<HttpResponseMessage> FakeServerCodeAsync(HttpMethod method, string relativeUri, HttpContent? content, CancellationToken cancellation)
		{
			string body = (content is { })
				? await content.ReadAsStringAsync(cancellation).ConfigureAwait(false)
				: "";

			Uri baseUri = new("http://127.0.0.1");
			Uri uri = new(baseUri, relativeUri);
			var parameters = HttpUtility.ParseQueryString(uri.Query);

			Assert.True(parameters.Count <= 10);

			IEnumerable<uint256> requestedTxIds = parameters["transactionIds"]!.Split(",").Select(x => uint256.Parse(x));
			IEnumerable<string> result = mempool.Where(tx => requestedTxIds.Contains(tx.GetHash())).Select(tx => tx.ToHex());

			var response = new HttpResponseMessage(HttpStatusCode.OK);
			response.Content = new StringContent(JsonConvert.SerializeObject(result));
			return response;
		}

		using var mockTorHttpClient = new MockHttpClient();
		mockTorHttpClient.OnSendAsync = req =>
			FakeServerCodeAsync(req.Method, req.RequestUri.ToString(), req.Content, CancellationToken.None);

		var client = new WasabiClient(mockTorHttpClient);
		Assert.Empty(WasabiClient.TransactionCache);

		// Requests one transaction
		var searchedTxId = mempool[0].GetHash();
		var txs = await client.GetTransactionsAsync(Network.Main, new[] { searchedTxId }, CancellationToken.None);

		Assert.Equal(searchedTxId, txs.First().GetHash());
		Assert.NotEmpty(WasabiClient.TransactionCache);
		Assert.True(WasabiClient.TransactionCache.ContainsKey(searchedTxId));

		// Requests 20 transaction
		var searchedTxIds = mempool[..20].Select(x => x.GetHash());
		txs = await client.GetTransactionsAsync(Network.Main, searchedTxIds, CancellationToken.None);
		Assert.Equal(20, txs.Count());

		// Requests 1100 transaction
		searchedTxIds = mempool.Select(x => x.GetHash());
		txs = await client.GetTransactionsAsync(Network.Main, searchedTxIds, CancellationToken.None);
		Assert.Equal(1_100, txs.Count());
		Assert.Equal(1_000, WasabiClient.TransactionCache.Count);

		Assert.Subset(WasabiClient.TransactionCache.Keys.ToHashSet(), txs.TakeLast(1_000).Select(x => x.GetHash()).ToHashSet());

		// Requests transactions that are already in the cache
		mockTorHttpClient.OnSendAsync = req =>
			Task.FromException<HttpResponseMessage>(new InvalidOperationException("The transaction should already be in the client cache. Http request was unexpected."));

		var expectedTobeCachedTxId = mempool.Last().GetHash();
		txs = await client.GetTransactionsAsync(Network.Main, new[] { expectedTobeCachedTxId }, CancellationToken.None);
		Assert.Equal(expectedTobeCachedTxId, txs.Last().GetHash());

		// Requests fails with Bad Request
		mockTorHttpClient.OnSendAsync = req =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
			{
				Content = new StringContent("\"Some RPC problem...\"")
			});

		var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
			await client.GetTransactionsAsync(Network.Main, new[] { RandomUtils.GetUInt256() }, CancellationToken.None));
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

	[Fact]
	public void ConstantsTests()
	{
		var min = int.Parse(WalletWasabi.Helpers.Constants.ClientSupportBackendVersionMin);
		var max = int.Parse(WalletWasabi.Helpers.Constants.ClientSupportBackendVersionMax);
		Assert.True(min <= max);

		int.Parse(WalletWasabi.Helpers.Constants.BackendMajorVersion);
	}
}
