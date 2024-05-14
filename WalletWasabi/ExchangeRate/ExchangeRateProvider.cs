using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ExchangeRateProviderInfo = (string Name, string ApiUrl, System.Func<string, decimal> Extractor);

namespace WalletWasabi.ExchangeRate;

public class ExchangeRateProvider
{
	private static ExchangeRateProviderInfo[] Providers = [
		("Bitstamp", "https://www.bitstamp.net/api/v2/ticker/btcusd", XPath(".bid")),
		("Blockchain", "https://blockchain.info/ticker", XPath(".USD.buy")),
		("Coinbase", "https://api.coinbase.com/v2/exchange-rates?currency=BTC", XPath(".data.rates.USD")),
		("CoinGecko", "https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&ids=bitcoin", XPath(".[0].current_price")),
		("Coingate", "https://api.coingate.com/v2/rates/merchant/BTC/USD", XPath("$")),
		("Gemini", "https://api.gemini.com/v1/pubticker/btcusd", XPath(".bid")),
	];

	public async Task<ExchangeRate> GetExchangeRateAsync(string providerName, CancellationToken cancellationToken)
	{
		var providerInfo = Providers.First(x => x.Name == providerName);
		var url = new Uri(providerInfo.ApiUrl);

#pragma warning disable RS0030 // Do not use banned APIs
		using var httpClient = new HttpClient();
		httpClient.BaseAddress = new Uri($"{url.Scheme}://{url.Host}");
		httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WasabiWallet", Helpers.Constants.ClientVersion.ToString()));
#pragma warning restore RS0030 // Do not use banned APIs
		using var response = await httpClient.GetAsync(url.PathAndQuery, cancellationToken).ConfigureAwait(false);
		var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		var rate = providerInfo.Extractor(json);
		return new ExchangeRate("USD", rate);
	}

	private static Func<string, decimal> XPath(string xpath) =>
		json =>
			JToken.Parse(json).SelectToken(xpath)?.Value<decimal>()
			?? throw new ArgumentException($@"The xpath {xpath} was not found.", nameof(xpath));

	private static Func<string, decimal> Plain() =>
		decimal.Parse;
}
