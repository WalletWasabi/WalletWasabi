using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Client.Wallets.Exchange;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallets.Exchange;

/// <summary>
/// Tests for <see cref="ExchangeRateProviders"/>.
/// </summary>
public class ExchangeRateProvidersTests
{
	private const string MempoolSpaceResponse = """{"time":1761300000,"USD":110000,"EUR":94000,"GBP":82000,"CAD":152000,"CHF":87000,"AUD":168000,"JPY":16800000}""";
	private const string BlockchainInfoResponse = """{"USD":{"15m":110000.5,"last":110000.5,"buy":110000.5,"sell":110000.5,"symbol":"$"},"EUR":{"15m":94000.5,"last":94000.5,"buy":94000.5,"sell":94000.5,"symbol":"€"},"BRL":{"15m":600000.5,"last":600000.5,"buy":600000.5,"sell":600000.5,"symbol":"R$"}}""";
	private const string CoinGeckoResponse = """{"bitcoin":{"aud":168000,"cad":152000,"chf":87000,"czk":2300000,"eur":94000,"gbp":82000,"jpy":16800000,"usd":110000}}""";
	private const string GeminiResponse = """[{"pair":"ALIUSD","price":"0.001037","percentChange24h":"-0.0161"},{"pair":"BTCUSD","price":"110000.75","percentChange24h":"0.01"},{"pair":"BTCUSDT","price":"110001","percentChange24h":"0.01"},{"pair":"BTCEUR","price":"94000.75","percentChange24h":"0.01"},{"pair":"BTCGUSD","price":"110002","percentChange24h":"0.01"}]""";

	[Fact]
	public void ProvidersAreFilteredByTheCurrencyTheyQuote()
	{
		Assert.Equal(["BlockchainInfo", "MempoolSpace", "CoinGecko", "Gemini", "None"], ExchangeRateProviders.ProviderNamesFor("USD"));
		Assert.Equal(["BlockchainInfo", "MempoolSpace", "CoinGecko", "None"], ExchangeRateProviders.ProviderNamesFor("JPY"));
		Assert.Equal(["CoinGecko", "None"], ExchangeRateProviders.ProviderNamesFor("CZK"));
	}

	[Fact]
	public async Task MempoolSpaceAsync()
	{
		// No known currency.
		{
			var provider = ExchangeRateProviders.MempoolSpaceAsync(FetcherReturning("""{"time":1761300000,"SEK":1000000}"""));
			await Assert.ThrowsAsync<InvalidOperationException>(() => provider(TestContext.Current.CancellationToken));
		}

		// All known currencies.
		{
			ExchangeRate[] expectedRates = [
				new("USD", 110000),
				new("EUR", 94000),
				new("GBP", 82000),
				new("CAD", 152000),
				new("CHF", 87000),
				new("AUD", 168000),
				new("JPY", 16800000)
				];

			var provider = ExchangeRateProviders.MempoolSpaceAsync(FetcherReturning(MempoolSpaceResponse));
			var actualRates = await provider(TestContext.Current.CancellationToken);

			Assert.Equal(expectedRates, actualRates);
		}
	}

	[Fact]
	public async Task BlockchainInfoAsync()
	{
		ExchangeRate[] expectedRates = [new("USD", 110000.5m), new("EUR", 94000.5m)];

		var provider = ExchangeRateProviders.BlockchainInfoAsync(FetcherReturning(BlockchainInfoResponse));
		var actualRates = await provider(TestContext.Current.CancellationToken);

		Assert.Equal(expectedRates, actualRates);
	}

	[Fact]
	public async Task CoinGeckoAsync()
	{
		Uri? requestedUrl = null;
		var provider = ExchangeRateProviders.CoinGeckoAsync(FetcherReturning(CoinGeckoResponse, url => requestedUrl = url));

		var exchangeRates = await provider(TestContext.Current.CancellationToken);

		Assert.Contains("vs_currencies=aud,cad,chf,czk,eur,gbp,jpy,usd", requestedUrl!.Query);
		Assert.Equal(ExchangeRateProviders.Currencies, exchangeRates.Select(x => x.Ticker));
		Assert.Equal(new ExchangeRate("CZK", 2300000), exchangeRates.ForCurrency("CZK"));
	}

	[Fact]
	public async Task GeminiAsync()
	{
		ExchangeRate[] expectedRates = [new("USD", 110000.75m), new("EUR", 94000.75m)];

		var provider = ExchangeRateProviders.GeminiAsync(FetcherReturning(GeminiResponse));
		var actualRates = await provider(TestContext.Current.CancellationToken);

		Assert.Equal(expectedRates, actualRates);
	}

	private static ExchangeRateHttpFetcher FetcherReturning(string json) =>
		FetcherReturning(json, _ => { });

	private static ExchangeRateHttpFetcher FetcherReturning(string json, Action<Uri> onRequest) =>
		(_, url, _) =>
		{
			onRequest(url);
			return Task.FromResult(json);
		};
}
