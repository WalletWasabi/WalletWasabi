using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WebClients;

namespace WalletWasabi.Client.Wallets.Exchange;

using ExchangeRatesExtractor = Func<string, ExchangeRate[]>;
public record ExchangeRatesChanged(ExchangeRate[] ExchangeRates);
public record ExchangeRateProviderInfo(string Name, ImmutableArray<string> Currencies);
public delegate Task<ExchangeRate[]> ExchangeRateProvider(CancellationToken cancellationToken);

/// <summary>Performs a HTTP GET request to the given URI and returns the response body.</summary>
public delegate Task<string> ExchangeRateHttpFetcher(string providerName, Uri url, CancellationToken cancellationToken);

public static class ExchangeRateProviders
{
	public static readonly ImmutableArray<string> Currencies =
	[
		"AUD",
		"CAD",
		"CHF",
		"CZK",
		"EUR",
		"GBP",
		"JPY",
		"USD",
	];

	public static readonly ImmutableDictionary<string, string> CurrencyNames =
		new Dictionary<string, string>
		{
			["AUD"] = "Australian dollar",
			["CAD"] = "Canadian dollar",
			["CHF"] = "Swiss franc",
			["CZK"] = "Czech koruna",
			["EUR"] = "Euro",
			["GBP"] = "British pound",
			["JPY"] = "Japanese yen",
			["USD"] = "United States dollar",
		}.ToImmutableDictionary();

	private static readonly ImmutableArray<string> BlockchainInfoCurrencies = ["USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD"];
	private static readonly ImmutableArray<string> MempoolSpaceCurrencies = ["USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD"];
	private static readonly ImmutableArray<string> GeminiCurrencies = ["USD", "EUR", "GBP"];

	public static readonly ImmutableArray<ExchangeRateProviderInfo> Providers =
	[
		new("BlockchainInfo", BlockchainInfoCurrencies),
		new("MempoolSpace", MempoolSpaceCurrencies),
		new("CoinGecko", Currencies),
		new("Gemini", GeminiCurrencies)
	];

	public static string[] ProviderNamesFor(string currency) =>
		[.. Providers.Where(x => x.Currencies.Contains(currency)).Select(x => x.Name), "None"];

	public static ExchangeRateProvider BlockchainInfoAsync(ExchangeRateHttpFetcher fetcher) =>
		cancellationToken => GetExchangeRatesAsync("BlockchainInfo", "https://blockchain.info/ticker", BlockchainInfoHandler(),
			fetcher, cancellationToken);

	public static ExchangeRateProvider MempoolSpaceAsync(ExchangeRateHttpFetcher fetcher) =>
		cancellationToken => GetExchangeRatesAsync("MempoolSpace", "https://mempool.space/api/v1/prices", MempoolSpaceHandler(),
			fetcher, cancellationToken);

	public static ExchangeRateProvider CoinGeckoAsync(ExchangeRateHttpFetcher fetcher) =>
		cancellationToken => GetExchangeRatesAsync("CoinGecko", $"https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies={string.Join(',', Currencies).ToLowerInvariant()}", CoinGeckoHandler(),
			fetcher, cancellationToken);

	public static ExchangeRateProvider GeminiAsync(ExchangeRateHttpFetcher fetcher) =>
		cancellationToken => GetExchangeRatesAsync("Gemini", "https://api.gemini.com/v1/pricefeed", GeminiHandler(),
			fetcher, cancellationToken);

	public static ExchangeRateProvider NoneAsync() =>
		_ => Task.FromResult<ExchangeRate[]>([]);

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
					// Ignore. Try the next provider.
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			throw new InvalidOperationException("All exchange rate providers failed to give us an exchange rate.");
		};

	private static async Task<ExchangeRate[]> GetExchangeRatesAsync(
		string providerName,
		string apiUrl,
		ExchangeRatesExtractor extractor,
		ExchangeRateHttpFetcher fetcher,
		CancellationToken cancellationToken)
	{
		var url = new Uri(apiUrl);
		var json = await fetcher(providerName, url, cancellationToken).ConfigureAwait(false);

		return Result<ExchangeRate[], Exception>
			.Catch(() => extractor(json))
			.Match(
				exchangeRates =>
				{
					if (exchangeRates.Length == 0)
					{
						throw new InvalidOperationException($"'{providerName}' quoted none of the known currencies.");
					}

					Logger.LogInfo($"Fetched exchange rates from {providerName}: {string.Join(", ", exchangeRates)}.");
					return exchangeRates;
				},
				e => throw new InvalidOperationException($"Error parsing exchange rate provider response. {e}"));
	}

	private static ExchangeRatesExtractor BlockchainInfoHandler() =>
		json => JsonDocument.Parse(json).RootElement
			.EnumerateObject()
			.Where(x => BlockchainInfoCurrencies.Contains(x.Name))
			.Select(x => new ExchangeRate(x.Name, x.Value.GetProperty("buy").GetDecimal()))
			.ToArray();

	private static ExchangeRatesExtractor MempoolSpaceHandler() =>
		json => JsonDocument.Parse(json).RootElement
			.EnumerateObject()
			.Where(x => MempoolSpaceCurrencies.Contains(x.Name))
			.Select(x => new ExchangeRate(x.Name, x.Value.GetDecimal()))
			.ToArray();

	private static ExchangeRatesExtractor CoinGeckoHandler() =>
		json => JsonDocument.Parse(json).RootElement
			.GetProperty("bitcoin")
			.EnumerateObject()
			.Select(x => new ExchangeRate(x.Name.ToUpperInvariant(), x.Value.GetDecimal()))
			.ToArray();

	private static ExchangeRatesExtractor GeminiHandler() =>
		json => JsonDocument.Parse(json).RootElement
			.EnumerateArray()
			.Select(x => (Pair: x.GetProperty("pair").GetString()!, Price: x.GetProperty("price").GetString()!))
			.Where(x => x.Pair.StartsWith("BTC"))
			.Select(x => (Currency: x.Pair[3..], x.Price))
			.Where(x => GeminiCurrencies.Contains(x.Currency))
			.Select(x => new ExchangeRate(x.Currency, decimal.Parse(x.Price, CultureInfo.InvariantCulture)))
			.ToArray();

	public static ExchangeRateHttpFetcher FetcherFactory(IHttpClientFactory factory) =>
		async (string providerName, Uri url, CancellationToken cancellationToken) =>
		{
			var userAgentPicker = UserAgent.GenerateUserAgentPicker();

			var httpClient = factory.CreateClient($"Exchange-rate-provider-{providerName}");
			httpClient.BaseAddress = new Uri($"{url.Scheme}://{url.Host}");
			httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgentPicker());

			using var response = await httpClient.GetAsync(url.PathAndQuery, cancellationToken).ConfigureAwait(false);
			response.EnsureSuccessStatusCode($"Error requesting exchange rate to '{providerName}'");
			return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		};
}
