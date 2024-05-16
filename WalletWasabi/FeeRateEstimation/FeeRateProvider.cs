using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;
using WalletWasabi.Tor.Socks5;

namespace WalletWasabi.FeeRateEstimation;
using FeeRateProviderInfo = (string Name, string ApiUrl, Func<string, AllFeeEstimate> Extractor);

public class FeeRateProvider(EndPoint? socksProxyEndPoint = null)
{
	public static readonly ImmutableArray<FeeRateProviderInfo> Providers = [
		("BlockstreamInfo", "https://blockstream.info/api/fee-estimates", BlockstreamHandler()),
		("MempoolSpace", "https://mempool.space/api/v1/fees/recommended", MempoolSpaceHandler()),
	];

	public async Task<AllFeeEstimate> GetFeeRateEstimationsAsync(string providerName, string userAgent, CancellationToken cancellationToken)
	{
		var providerInfo = Providers.FirstOrDefault(x => x.Name.Equals(providerName, StringComparison.InvariantCultureIgnoreCase));
		if (providerInfo == default)
		{
			throw new NotSupportedException($"Fee rate estimations provider '{providerName}' is not supported.");
		}
		var url = new Uri(providerInfo.ApiUrl);

#pragma warning disable RS0030 // Do not use banned APIs
		using var httpClientHandler = new HttpClientHandler();
		using var httpClient = new HttpClient(httpClientHandler);
		httpClientHandler.Proxy = Socks5Proxy.GetWebProxy(socksProxyEndPoint);
		httpClient.BaseAddress = new Uri($"{url.Scheme}://{url.Host}");
		httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
#pragma warning restore RS0030 // Do not use banned APIs

		using var response = await httpClient.GetAsync(url.PathAndQuery, cancellationToken).ConfigureAwait(false);
		var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		var estimations = providerInfo.Extractor(json);
		return estimations;
	}

	private static Func<string, AllFeeEstimate> BlockstreamHandler() =>
		json => new AllFeeEstimate(
			JsonDocument.Parse(json).RootElement
				.EnumerateObject()
				.Select(o => (int.Parse(o.Name), (int) Math.Ceiling(o.Value.GetDouble())))
				.ToDictionary());

	private static Func<string, AllFeeEstimate> MempoolSpaceHandler() =>
		json =>
			new AllFeeEstimate(
				JsonDocument.Parse(json).RootElement
					.EnumerateObject()
					.Select(o => (o.Name, o.Value.GetDouble()) switch {
							("fastestFee", var feeRate) => (Target: 2, feeRate),
							("halfHourFee", var feeRate) => (Target: 3, feeRate),
							("hourFee", var feeRate) => (Target: 6, feeRate),
							("economyFee", var feeRate) => (Target: 72, feeRate),
							_ => (0, 0)
						})
					.Where(x => x.Target > 0)
					.Select(o => (o.Target, (int) Math.Ceiling(o.feeRate)))
					.ToDictionary());
}
