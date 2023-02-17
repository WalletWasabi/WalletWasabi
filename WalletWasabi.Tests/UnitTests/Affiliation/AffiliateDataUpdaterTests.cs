using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Language;
using NBitcoin;
using WalletWasabi.Affiliation;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor.Http;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class AffiliateDataUpdaterTests
{
	public (Mock<IHttpClient> mockHttpClient, ISetupSequentialResult<Task<HttpResponseMessage>> setup) CreateMockHttpClient()
	{
		var mockHttpClient = new Mock<IHttpClient>();
		var setup = mockHttpClient.SetupSequence(
			httpClient => httpClient.SendAsync(
				HttpMethod.Post,
				"notify_coinjoin",
				It.IsAny<HttpContent>(),
				It.IsAny<CancellationToken>()));
		return (mockHttpClient, setup);
	}

	[Fact]
	public async Task GetCoinJoinRequestTestAsync()
	{
		static HttpResponseMessage Ok()
		{
			HttpResponseMessage okResponse = new(HttpStatusCode.OK);
			okResponse.Content = new StringContent("{ \"affiliate_data\":\"010203040506\" }");
			return okResponse;
		}

		using HttpResponseMessage ok0 = Ok();
		using HttpResponseMessage ok1 = Ok();
		var (clientMock, setup) = CreateMockHttpClient();
		setup
			.ReturnsAsync(ok0)
			.ReturnsAsync(ok1);

		Dictionary<string, AffiliateServerHttpApiClient> servers = new ()
		{
			["affiliate"] = new AffiliateServerHttpApiClient(clientMock.Object),
		};

		using CancellationTokenSource testCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		using AffiliationMessageSigner signer = new AffiliationMessageSigner(WalletWasabi.Helpers.Constants.FallbackAffiliationMessageSignerKey);
		var notifications = new AsyncQueue<RoundNotification>();
		var notifier = new Mock<IRoundNotifier>();
		notifier
			.Setup(x => x.GetRoundNotifications(It.IsAny<CancellationToken>()))
			.Returns(notifications.GetAsyncIterator(testCts.Token));

		using AffiliateDataUpdater requestsUpdater = new (notifier.Object, servers.ToImmutableDictionary(), signer);
		try
		{
			await requestsUpdater.StartAsync(testCts.Token);

			// Remove an non-existing round. Expected result: nothing happens
			notifications.Enqueue(new RoundEndedNotification(uint256.One));
			Assert.Empty(requestsUpdater.GetAffiliateData());

			// Notify about a new built coinjoin
			var wasabiCoin = WabiSabiFactory.CreateCoin();
			var affiliateCoin = WabiSabiFactory.CreateCoin();
			var destination = BitcoinFactory.CreateScript();
			var coinjoinData = new BuiltTransactionData(
				new[]
				{
					new AffiliateInput(
						wasabiCoin.Outpoint,
						wasabiCoin.ScriptPubKey,
						AffiliationConstants.DefaultAffiliationId,
						false),
					new AffiliateInput(affiliateCoin.Outpoint, affiliateCoin.ScriptPubKey, "affiliate", false)
				},
				new[]
				{
					new TxOut(wasabiCoin.Amount + affiliateCoin.Amount, destination)
				},
				Network.TestNet,
				CoordinationFeeRate.Zero,
				Money.Zero);

			notifications.Enqueue(new RoundBuiltTransactionNotification(uint256.One, coinjoinData));
			await Task.Delay(500); // this is to give time to the notification to be consumed.
			var coinjoinRequests = Assert.Single(requestsUpdater.GetAffiliateData());

			Assert.Equal(uint256.One, uint256.Parse(coinjoinRequests.Key));
			var coinjoinRequest = Assert.Single(coinjoinRequests.Value);
			Assert.Equal("affiliate", coinjoinRequest.Key);
			Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, coinjoinRequest.Value);

			testCts.Cancel();
		}
		finally
		{
			await requestsUpdater.StopAsync(testCts.Token);
		}
	}
}
