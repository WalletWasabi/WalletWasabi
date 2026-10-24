using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.Wallets.Exchange;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallets.Exchange;

public class ExchangeRateProvidersTests
{
	private const string MempoolSpaceResponse = """{"time":1761300000,"USD":110000,"EUR":94000,"GBP":82000,"CAD":152000,"CHF":87000,"AUD":168000,"JPY":16800000}""";
	private const string BlockchainInfoResponse = """{"USD":{"15m":110000.5,"last":110000.5,"buy":110000.5,"sell":110000.5,"symbol":"$"},"EUR":{"15m":94000.5,"last":94000.5,"buy":94000.5,"sell":94000.5,"symbol":"€"}}""";
	private const string CoinGeckoResponse = """[{"id":"bitcoin","symbol":"btc","current_price":94000.25}]""";
	private const string GeminiResponse = """{"symbol":"BTCEUR","bid":"94000.75","ask":"94001.00","last":"94000.80"}""";

	[Theory]
	[InlineData("USD", 110000)]
	[InlineData("EUR", 94000)]
	[InlineData("JPY", 16800000)]
	public async Task MempoolSpaceReturnsTheRequestedCurrencyAsync(string currency, decimal expectedRate)
	{
		var provider = ExchangeRateProviders.MempoolSpaceAsync(FactoryReturning(MempoolSpaceResponse), currency);

		var exchangeRate = await provider(CancellationToken.None);

		Assert.Equal(new ExchangeRate(currency, expectedRate), exchangeRate);
	}

	[Fact]
	public async Task BlockchainInfoReturnsTheRequestedCurrencyAsync()
	{
		var provider = ExchangeRateProviders.BlockchainInfoAsync(FactoryReturning(BlockchainInfoResponse), "EUR");

		var exchangeRate = await provider(CancellationToken.None);

		Assert.Equal(new ExchangeRate("EUR", 94000.5m), exchangeRate);
	}

	[Fact]
	public async Task CoinGeckoRequestsTheCurrencyInLowerCaseAsync()
	{
		HttpRequestMessage? request = null;
		var provider = ExchangeRateProviders.CoinGeckoAsync(FactoryReturning(CoinGeckoResponse, r => request = r), "EUR");

		var exchangeRate = await provider(CancellationToken.None);

		Assert.Equal(new ExchangeRate("EUR", 94000.25m), exchangeRate);
		Assert.Contains("vs_currency=eur", request!.RequestUri!.Query);
	}

	[Fact]
	public async Task GeminiRequestsThePairOfTheCurrencyAsync()
	{
		HttpRequestMessage? request = null;
		var provider = ExchangeRateProviders.GeminiAsync(FactoryReturning(GeminiResponse, r => request = r), "EUR");

		var exchangeRate = await provider(CancellationToken.None);

		Assert.Equal(new ExchangeRate("EUR", 94000.75m), exchangeRate);
		Assert.EndsWith("/v1/pubticker/btceur", request!.RequestUri!.AbsolutePath);
	}

	[Fact]
	public async Task CurrencyMissingFromTheResponseIsAnErrorAsync()
	{
		var provider = ExchangeRateProviders.MempoolSpaceAsync(FactoryReturning(MempoolSpaceResponse), "SEK");

		await Assert.ThrowsAsync<InvalidOperationException>(() => provider(CancellationToken.None));
	}

	private static MockHttpClientFactory FactoryReturning(string json) =>
		FactoryReturning(json, _ => { });

	// The providers use HttpClient.GetAsync, which bypasses the SendAsync overload that MockHttpClient overrides,
	// so the canned response has to be served from the message handler.
	private static MockHttpClientFactory FactoryReturning(string json, Action<HttpRequestMessage> onRequest) =>
		new() { OnCreateClient = _ => new HttpClient(new CannedResponseHandler(json, onRequest)) };

	private class CannedResponseHandler(string json, Action<HttpRequestMessage> onRequest) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			onRequest(request);
			return Task.FromResult(HttpResponseMessageEx.Ok(json));
		}
	}
}
