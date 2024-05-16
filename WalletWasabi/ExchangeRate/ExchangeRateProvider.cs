using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ExchangeRateProviderInfo = (string Name, bool ClearNetOnly, string ApiUrl, System.Func<string, decimal> Extractor);

namespace WalletWasabi.ExchangeRate;

public class ExchangeRateProvider(EndPoint? socksProxyEndPoint = null)
{
	public static readonly ImmutableArray<ExchangeRateProviderInfo> Providers =
	[
		("bitstamp", true, "https://www.bitstamp.net/api/v2/ticker/btcusd", JsonPath(".bid")),
		("blockchain", false, "https://blockchain.info/ticker", JsonPath(".USD.buy")),
		("coinbase", true, "https://api.coinbase.com/v2/exchange-rates?currency=BTC", JsonPath(".data.rates.USD")),
		("coingecko", false, "https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&ids=bitcoin", JsonPath(".[0].current_price")),
		("coingate", true, "https://api.coingate.com/v2/rates/merchant/BTC/USD", JsonPath("$")),
		("gemini", false, "https://api.gemini.com/v1/pubticker/btcusd", JsonPath(".bid"))
	];

	public async Task<ExchangeRate> GetExchangeRateAsync(string providerName, string userAgent, CancellationToken cancellationToken)
	{
		var providerInfo = Providers.FirstOrDefault(x => x.Name.Equals(providerName, StringComparison.InvariantCultureIgnoreCase));
		if (providerInfo == default)
		{
			throw new NotSupportedException($"Exchange rate provider '{providerName}' is not supported.");
		}
		var proxy = GetWebProxy();
		if (providerInfo.ClearNetOnly && proxy is not null)
		{
			throw new NotSupportedException($"Exchange rate provider '{providerName}' cannot be reached with Tor, only clearnet.");
		}

		var url = new Uri(providerInfo.ApiUrl);

#pragma warning disable RS0030 // Do not use banned APIs
		using var httpClientHandler = new HttpClientHandler();
		using var httpClient = new HttpClient(httpClientHandler);
		httpClientHandler.Proxy = proxy;
		httpClient.BaseAddress = new Uri($"{url.Scheme}://{url.Host}");
		httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
#pragma warning restore RS0030 // Do not use banned APIs

		using var response = await httpClient.GetAsync(url.PathAndQuery, cancellationToken).ConfigureAwait(false);
		var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		var rate = providerInfo.Extractor(json);
		return new ExchangeRate("USD", rate);
	}

	private WebProxy? GetWebProxy()
	{
		return socksProxyEndPoint switch
		{
			DnsEndPoint dns => TorWebProxy(dns.Host, dns.Port),
			IPEndPoint ip => TorWebProxy(ip.Address.ToString(), ip.Port),
			null => null,
			_ => throw new NotSupportedException("The endpoint type is not supported.")
		};
		static WebProxy TorWebProxy(string host, int port) => new(new UriBuilder("socks5", host, port).Uri);
	}

	private static Func<string, decimal> JsonPath(string xpath) =>
		json =>
			JToken.Parse(json).SelectToken(xpath)?.Value<decimal>()
			?? throw new ArgumentException($@"The xpath {xpath} was not found.", nameof(xpath));
}
