using System.Collections.Immutable;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WebClients;

namespace WalletWasabi.Wallets.Exchange;

using ExchangeRateExtractor = Func<string, decimal>;
public delegate Task<ExchangeRate> ExchangeRateProvider(CancellationToken cancellationToken);

public static class ExchangeRateProviders
{
	public static readonly ImmutableArray<string> Providers =
	[
		"BlockstreamInfo",
		"MempoolSpace",
		"CoinGecko",
		"Gemini",
		"None"
	];

	private static UserAgentPicker PickRandomUserAgent = UserAgent.GenerateUserAgentPicker(false);

	public static ExchangeRateProvider BlockstreamAsync(IHttpClientFactory httpClientFactory) =>
		cancellationToken => GetExchangeRateAsync("Blockstream", "https://blockchain.info/ticker", JsonPath(".USD.buy"),
			httpClientFactory, PickRandomUserAgent(), cancellationToken);

	public static ExchangeRateProvider MempoolSpaceAsync(IHttpClientFactory httpClientFactory) =>
		cancellationToken => GetExchangeRateAsync("MempoolSpace", "https://mempool.space/api/v1/prices", JsonPath(".USD"),
			httpClientFactory, PickRandomUserAgent(), cancellationToken);

	public static ExchangeRateProvider CoinGeckoAsync(IHttpClientFactory httpClientFactory) =>
		cancellationToken => GetExchangeRateAsync("CoinGecko", "https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&ids=bitcoin", JsonPath(".[0].current_price"),
			httpClientFactory, PickRandomUserAgent(), cancellationToken);

	public static ExchangeRateProvider GeminiAsync(IHttpClientFactory httpClientFactory) =>
		cancellationToken => GetExchangeRateAsync("CoinGecko", "https://api.gemini.com/v1/pubticker/btcusd", JsonPath(".bid"),
			httpClientFactory, PickRandomUserAgent(), cancellationToken);

	public static ExchangeRateProvider NoneAsync() =>
		_ => Task.FromResult(new ExchangeRate("USD", -1m));

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

	private static async Task<ExchangeRate> GetExchangeRateAsync(string providerName, string apiUrl, ExchangeRateExtractor extractor, IHttpClientFactory httpClientFactory, string userAgent, CancellationToken cancellationToken)
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
					Logger.LogInfo($"Fetched exchange rate from {providerName}: {rate}.");
					return new ExchangeRate("USD", rate);
				},
				e => throw new InvalidOperationException($"Error parsing exchange rate provider response. {e}"));
	}

	private static ExchangeRateExtractor JsonPath(string xpath) =>
		json =>
			JToken.Parse(json).SelectToken(xpath)?.Value<decimal>()
			?? throw new ArgumentException($@"The xpath {xpath} was not found.", nameof(xpath));
}
