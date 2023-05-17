using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using WalletWasabi.Affiliation;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor.Http;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class AffiliateDataUpdaterTests
{
	[Fact]
	public async Task GetCoinJoinRequestTestAsync()
	{
		var trezorClient = new AffiliateHttpClient("http://trezor.io")
		{
			OnSendAsync = async (message, token) =>
			{
				var requestText = await message.Content?.ReadAsStringAsync(token)!;
				Assert.Contains("\"is_affiliated\":true", requestText);
				HttpResponseMessage okResponse = new(HttpStatusCode.OK);
				okResponse.Content = new StringContent("{ \"affiliate_data\":\"010203040506\" }");
				return okResponse;
			}
		};

		var btcpayClient = new AffiliateHttpClient("http://btcpayserver.julio.net")
		{
			OnSendAsync = async (message, token) =>
			{
				var requestText = await message.Content?.ReadAsStringAsync(token)!;
				Assert.DoesNotContain("\"is_affiliated\":true", requestText);

				HttpResponseMessage okResponse = new(HttpStatusCode.OK);
				okResponse.Content = new StringContent("{}");
				return okResponse;
			}
		};

		Dictionary<string, AffiliateServerHttpApiClient> servers = new()
		{
			["trezor"] = new AffiliateServerHttpApiClient(trezorClient),
			["btcpay"] = new AffiliateServerHttpApiClient(btcpayClient),
		};

		using CancellationTokenSource testCts = new(TimeSpan.FromSeconds(10));
		using AffiliationMessageSigner signer = new(WalletWasabi.Helpers.Constants.FallbackAffiliationMessageSignerKey);
		var notifications = new AsyncQueue<RoundNotification>();
		var notifier = new Mock<IRoundNotifier>();
		notifier
			.Setup(x => x.GetRoundNotifications(It.IsAny<CancellationToken>()))
			.Returns(notifications.GetAsyncIterator(testCts.Token));

		using AffiliateDataUpdater requestsUpdater = new(notifier.Object, servers.ToImmutableDictionary(), signer);
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
						wasabiCoin.Amount,
						AffiliationConstants.DefaultAffiliationId,
						false),
					new AffiliateInput(affiliateCoin.Outpoint, affiliateCoin.ScriptPubKey, affiliateCoin.Amount, "trezor", false)
				},
				new[]
				{
					new TxOut(wasabiCoin.Amount + affiliateCoin.Amount, destination)
				},
				Network.TestNet,
				CoordinationFeeRate.Zero,
				Money.Zero);

			notifications.Enqueue(new RoundBuiltTransactionNotification(RoundId: uint256.One, TxId: uint256.Zero, coinjoinData));
			await Task.Delay(500); // this is to give time to the notification to be consumed.
			var coinjoinRequests = Assert.Single(requestsUpdater.GetAffiliateData());

			Assert.Equal(uint256.One, uint256.Parse(coinjoinRequests.Key));
			var coinjoinRequestTrezor = coinjoinRequests.Value["trezor"];
			Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, coinjoinRequestTrezor);

			var coinjoinRequestBtcPay = coinjoinRequests.Value["btcpay"];
			Assert.Null(coinjoinRequestBtcPay);

			testCts.Cancel();
		}
		finally
		{
			await requestsUpdater.StopAsync(testCts.Token);
		}
	}

	public class AffiliateHttpClient : IHttpClient
	{
		public AffiliateHttpClient(string server)
		{
			BaseUriGetter = () => new Uri(server);
		}

		public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> OnSendAsync { get; set; }
		public Func<Uri>? BaseUriGetter { get; }

		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
		{
			return OnSendAsync(request, cancellationToken);
		}
	}
}
