using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Client.Wallets.Exchange;
using WalletWasabi.Services;
using WalletWasabi.Tests.UnitTests.Mocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallets.Exchange;

public class ExchangeRateProvidersTests
{
	private const string MempoolSpaceResponse = """{"time":1761300000,"USD":110000,"EUR":94000,"GBP":82000,"CAD":152000,"CHF":87000,"AUD":168000,"JPY":16800000}""";
	private const string BlockchainInfoResponse = """{"USD":{"15m":110000.5,"last":110000.5,"buy":110000.5,"sell":110000.5,"symbol":"$"},"EUR":{"15m":94000.5,"last":94000.5,"buy":94000.5,"sell":94000.5,"symbol":"€"},"BRL":{"15m":600000.5,"last":600000.5,"buy":600000.5,"sell":600000.5,"symbol":"R$"}}""";
	private const string CoinGeckoResponse = """{"bitcoin":{"usd":110000,"eur":94000,"gbp":82000,"jpy":16800000,"chf":87000,"cad":152000,"aud":168000,"czk":2300000}}""";
	private const string GeminiResponse = """[{"pair":"ALIUSD","price":"0.001037","percentChange24h":"-0.0161"},{"pair":"BTCUSD","price":"110000.75","percentChange24h":"0.01"},{"pair":"BTCUSDT","price":"110001","percentChange24h":"0.01"},{"pair":"BTCEUR","price":"94000.75","percentChange24h":"0.01"},{"pair":"BTCGUSD","price":"110002","percentChange24h":"0.01"}]""";

	[Fact]
	public async Task MempoolSpaceQuotesEveryKnownCurrencyOfTheResponseAsync()
	{
		var provider = ExchangeRateProviders.MempoolSpaceAsync(FactoryReturning(MempoolSpaceResponse));

		var exchangeRates = await provider(CancellationToken.None);

		ExchangeRate[] expected = [new("USD", 110000), new("EUR", 94000), new("GBP", 82000), new("CAD", 152000), new("CHF", 87000), new("AUD", 168000), new("JPY", 16800000)];
		Assert.Equal(expected, exchangeRates);
	}

	[Fact]
	public async Task BlockchainInfoQuotesTheBuyPriceOfKnownCurrenciesOnlyAsync()
	{
		var provider = ExchangeRateProviders.BlockchainInfoAsync(FactoryReturning(BlockchainInfoResponse));

		var exchangeRates = await provider(CancellationToken.None);

		ExchangeRate[] expected = [new("USD", 110000.5m), new("EUR", 94000.5m)];
		Assert.Equal(expected, exchangeRates);
	}

	[Fact]
	public async Task CoinGeckoRequestsAllCurrenciesInOneCallAsync()
	{
		HttpRequestMessage? request = null;
		var provider = ExchangeRateProviders.CoinGeckoAsync(FactoryReturning(CoinGeckoResponse, r => request = r));

		var exchangeRates = await provider(CancellationToken.None);

		Assert.Contains("vs_currencies=usd,eur,gbp,jpy,chf,cad,aud,czk", request!.RequestUri!.Query);
		Assert.Equal(ExchangeRateProviders.Currencies, exchangeRates.Select(x => x.Ticker));
		Assert.Equal(new ExchangeRate("CZK", 2300000), exchangeRates.ForCurrency("CZK"));
	}

	[Fact]
	public async Task GeminiQuotesTheBitcoinPairsOfKnownCurrenciesOnlyAsync()
	{
		HttpRequestMessage? request = null;
		var provider = ExchangeRateProviders.GeminiAsync(FactoryReturning(GeminiResponse, r => request = r));

		var exchangeRates = await provider(CancellationToken.None);

		Assert.EndsWith("/v1/pricefeed", request!.RequestUri!.AbsolutePath);
		ExchangeRate[] expected = [new("USD", 110000.75m), new("EUR", 94000.75m)];
		Assert.Equal(expected, exchangeRates);
	}

	[Fact]
	public async Task ResponseWithoutAnyKnownCurrencyIsAnErrorAsync()
	{
		var provider = ExchangeRateProviders.MempoolSpaceAsync(FactoryReturning("""{"time":1761300000,"SEK":1000000}"""));

		await Assert.ThrowsAsync<InvalidOperationException>(() => provider(CancellationToken.None));
	}

	[Fact]
	public async Task NoneQuotesNothingAsync()
	{
		var provider = ExchangeRateProviders.NoneAsync();
		var exchangeRates = await provider(CancellationToken.None);

		Assert.Empty(exchangeRates);
	}

	[Fact]
	public void ProvidersAreFilteredByTheCurrencyTheyQuote()
	{
		Assert.Equal(["BlockchainInfo", "MempoolSpace", "CoinGecko", "Gemini", "None"], ExchangeRateProviders.ProviderNamesFor("USD"));
		Assert.Equal(["BlockchainInfo", "MempoolSpace", "CoinGecko", "None"], ExchangeRateProviders.ProviderNamesFor("JPY"));
		Assert.Equal(["CoinGecko", "None"], ExchangeRateProviders.ProviderNamesFor("CZK"));
	}

	[Fact]
	public void LookupByCurrency()
	{
		ExchangeRate[] exchangeRates = [new("USD", 110000), new("EUR", 94000)];

		Assert.Equal(new ExchangeRate("EUR", 94000), exchangeRates.ForCurrency("EUR"));
		Assert.Null(exchangeRates.ForCurrency("CZK"));
	}

	[Fact]
	public async Task UpdaterPublishesOnlyWhenTheRatesChangeAsync()
	{
		ExchangeRate[] quoted = [new("USD", 110000)];
		var eventBus = new EventBus();
		var published = 0;
		using var subscription = eventBus.Subscribe<ExchangeRatesChanged>(_ => published++);
		var update = ExchangeRateUpdater.CreateExchangeRateUpdater(_ => Task.FromResult(quoted), eventBus);
		var message = new ExchangeRateUpdater.UpdateMessage();

		var rates = await update(message, [], CancellationToken.None);
		rates = await update(message, rates, CancellationToken.None);
		Assert.Equal(1, published);

		quoted = [new("USD", 111000)];
		rates = await update(message, rates, CancellationToken.None);
		Assert.Equal(2, published);
		Assert.Equal(quoted, rates);

		quoted = [];
		rates = await update(message, rates, CancellationToken.None);
		Assert.Equal(2, published);
		Assert.Equal(new ExchangeRate("USD", 111000), rates.ForCurrency("USD"));
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
