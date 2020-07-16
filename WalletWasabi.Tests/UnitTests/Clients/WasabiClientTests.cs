using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	public class WasabiClientTests
	{
		[Fact]
		public async Task GetTransactionsTestAsync()
		{
			var mempool = Enumerable.Range(0, 1_100).Select(_ => CreateTransaction()).ToArray();

			Task<HttpResponseMessage> FakeServerCode(HttpMethod method, string action, NameValueCollection parameters, string body)
			{
				Assert.True(parameters.Count <= 10);

				IEnumerable<uint256> requestedTxIds = parameters["transactionIds"].Split(",").Select(x => uint256.Parse(x));
				IEnumerable<string> result = mempool.Where(tx => requestedTxIds.Contains(tx.GetHash())).Select(tx => tx.ToHex());

				var response = new HttpResponseMessage(HttpStatusCode.OK);
				response.Content = new StringContent(JsonConvert.SerializeObject(result));
				return Task.FromResult(response);
			};

			var torHttpClient = new MockTorHttpClient();
			torHttpClient.OnSendAsync = FakeServerCode;
			var client = new WasabiClient(torHttpClient);
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
			Assert.Equal(1_000, WasabiClient.TransactionCache.Count());

			Assert.Subset(WasabiClient.TransactionCache.Keys.ToHashSet(), txs.TakeLast(1_000).Select(x => x.GetHash()).ToHashSet());

			// Requests transactions that are already in the cache
			torHttpClient.OnSendAsync = (verb, action, parameters, body) =>
				Task.FromException<HttpResponseMessage>(
					new InvalidOperationException("The transaction should already be in the client cache. Http request was unexpected."));

			var expectedTobeCachedTxId = mempool.Last().GetHash();
			txs = await client.GetTransactionsAsync(Network.Main, new[] { expectedTobeCachedTxId }, CancellationToken.None);
			Assert.Equal(expectedTobeCachedTxId, txs.Last().GetHash());

			// Requests fails with Bad Request
			torHttpClient.OnSendAsync = (verb, action, parameters, body) =>
			{
				var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
				response.Content = new StringContent("\"Some RPC problem...\"");
				return Task.FromResult(response);
			};

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

		[Fact]
		public async Task SingleInstanceTestsAsync()
		{
			// Disposal test.
			using (SingleInstanceChecker sic = new SingleInstanceChecker(Network.Main))
			{
				await sic.CheckAsync().ConfigureAwait(false);
			}

			// Check different networks.
			using (SingleInstanceChecker sic = new SingleInstanceChecker(Network.Main))
			{
				await sic.CheckAsync().ConfigureAwait(false);
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sic.CheckAsync().ConfigureAwait(false));

				using SingleInstanceChecker sic2 = new SingleInstanceChecker(Network.Main);
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sic2.CheckAsync().ConfigureAwait(false));

				using SingleInstanceChecker sicTest = new SingleInstanceChecker(Network.TestNet);
				await sicTest.CheckAsync().ConfigureAwait(false);
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicTest.CheckAsync().ConfigureAwait(false));

				using SingleInstanceChecker sicReg = new SingleInstanceChecker(Network.RegTest);
				await sicReg.CheckAsync().ConfigureAwait(false);
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicReg.CheckAsync().ConfigureAwait(false));
			}
		}
	}
}
