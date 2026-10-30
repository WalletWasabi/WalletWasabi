using System.Net.Http;
using Newtonsoft.Json.Linq;
using WalletWasabi.WebClients;

namespace WalletWasabi.Wallets.Exchange;

using ExchangeRateExtractor = Func<string, decimal>;
public delegate Task<ExchangeRate> ExchangeRateProvider(CancellationToken cancellationToken);

public static class ExchangeRateProviders
{
	public static readonly ImmutableArray<string> Providers =
	[
		"BlockchainInfo",
		"MempoolSpace",
		"CoinGecko",
		"Gemini",
		"None"
	];

	public static readonly ImmutableArray<string> Currencies =
	[
		"USD",
		"EUR",
		"GBP",
		"JPY",
		"CHF",
		"CAD",
		"AUD"
	];

	private static UserAgentPicker PickRandomUserAgent = UserAgent.GenerateUserAgentPicker();

	public static ExchangeRateProvider BlockchainInfoAsync(IHttpClientFactory httpClientFactory, string currency) =>
		cancellationToken => GetExchangeRateAsync("BlockchainInfo", "https://blockchain.info/ticker", JsonPath($".{currency}.buy"), currency,
			httpClientFactory, PickRandomUserAgent(), cancellationToken);

	public static ExchangeRateProvider MempoolSpaceAsync(IHttpClientFactory httpClientFactory, string currency) =>
		cancellationToken => GetExchangeRateAsync("MempoolSpace", "https://mempool.space/api/v1/prices", JsonPath($".{currency}"), currency,
			httpClientFactory, PickRandomUserAgent(), cancellationToken);

	public static ExchangeRateProvider CoinGeckoAsync(IHttpClientFactory httpClientFactory, string currency) =>
		cancellationToken => GetExchangeRateAsync("CoinGecko", $"https://api.coingecko.com/api/v3/coins/markets?vs_currency={currency.ToLowerInvariant()}&ids=bitcoin", JsonPath(".[0].current_price"), currency,
			httpClientFactory, PickRandomUserAgent(), cancellationToken);

	public static ExchangeRateProvider GeminiAsync(IHttpClientFactory httpClientFactory, string currency) =>
		cancellationToken => GetExchangeRateAsync("Gemini", $"https://api.gemini.com/v1/pubticker/btc{currency.ToLowerInvariant()}", JsonPath(".bid"), currency,
			httpClientFactory, PickRandomUserAgent(), cancellationToken);

	public static ExchangeRateProvider NoneAsync(string currency) =>
		_ => Task.FromResult(new ExchangeRate(currency, -1m));

	public static ExchangeRateProvider Composed(ExchangeRateProvider[] exchangeRateProviders) =>
		async cancellationToken =>
		{
			foreach (var provider in exchangeRateProviders)
			{
				try
				{
					return await provider(cancellationToken).ConfigureAwait(false);
				}
				catch (Exception)
				{
					// ignore. Try the next provider
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			throw new InvalidOperationException("All exchange rate providers failed to give us an exchange rate.");
		};

	private static async Task<ExchangeRate> GetExchangeRateAsync(string providerName, string apiUrl, ExchangeRateExtractor extractor, string currency, IHttpClientFactory httpClientFactory, string userAgent, CancellationToken cancellationToken)
	{
		var url = new Uri(apiUrl);

		var httpClient = httpClientFactory.CreateClient($"{providerName}-exchange-rate-provider");
		httpClient.BaseAddress = new Uri($"{url.Scheme}://{url.Host}");
		httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);

		using var response = await httpClient.GetAsync(url.PathAndQuery, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode($"Error requesting exchange rate to '{providerName}'");
		var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		Logger.LogDebug(json);
		return Result<decimal, Exception>
			.Catch(() => extractor(json))
			.Match(
				rate =>
				{
					Logger.LogInfo($"Fetched BTC/{currency} exchange rate from {providerName}: {rate}.");
					return new ExchangeRate(currency, rate);
				},
				e => throw new InvalidOperationException($"Error parsing exchange rate provider response. {e}"));
	}

	private static ExchangeRateExtractor JsonPath(string xpath) =>
		json =>
			JToken.Parse(json).SelectToken(xpath)?.Value<decimal>()
			?? throw new ArgumentException($@"The xpath {xpath} was not found.", nameof(xpath));
}
