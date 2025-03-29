using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using ExchangeRateProviderInfo = (string Name, string ApiUrl, System.Func<string, decimal> Extractor);

namespace WalletWasabi.Wallets.Exchange;

public class ExchangeRateProvider(IHttpClientFactory httpClientFactory)
{
	public static readonly ImmutableArray<ExchangeRateProviderInfo> Providers =
	[
		("MempoolSpace", "https://mempool.space/api/v1/prices", JsonPath(".USD")),
		("BlockchainInfo", "https://blockchain.info/ticker", JsonPath(".USD.buy")),
		("CoinGecko", "https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&ids=bitcoin", JsonPath(".[0].current_price")),
		("Gemini", "https://api.gemini.com/v1/pubticker/btcusd", JsonPath(".bid")),
		("None", "", _ => 0),
	];

	public async Task<ExchangeRate> GetExchangeRateAsync(string providerName, string userAgent, CancellationToken cancellationToken)
	{
		var providerInfo = Providers.FirstOrDefault(x => x.Name.Equals(providerName, StringComparison.InvariantCultureIgnoreCase));
		if (providerInfo == default)
		{
			throw new NotSupportedException($"Exchange rate provider '{providerName}' is not supported.");
		}

		if(providerInfo.Name is "None" or "")
		{
			return new ExchangeRate("USD", -1);
		}

		var url = new Uri(providerInfo.ApiUrl);

		var httpClient = httpClientFactory.CreateClient($"{providerName}-exchange-rate-provider");
		httpClient.BaseAddress = new Uri($"{url.Scheme}://{url.Host}");
		httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);

		using var response = await httpClient.GetAsync(url.PathAndQuery, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode($"Error requesting exchange rate to '{providerName}'");
		var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		Logger.LogDebug(json);
		return Result<decimal, Exception>
			.Catch(() => providerInfo.Extractor(json))
			.Match(
				rate => new ExchangeRate("USD", rate),
				e => throw new InvalidOperationException($"Error parsing exchange rate provider response. {e}"));
	}

	private static Func<string, decimal> JsonPath(string xpath) =>
		json =>
			JToken.Parse(json).SelectToken(xpath)?.Value<decimal>()
			?? throw new ArgumentException($@"The xpath {xpath} was not found.", nameof(xpath));
}
