using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.FeeRateEstimation;
using FeeRateProviderInfo = (string Name, (string ClearNet, string Onion) ApiDomain, string ApiEndPoint, Func<string, FeeRateEstimations> Extractor);

public class FeeRateProvider(IHttpClientFactory httpClientFactory)
{
	public static readonly ImmutableArray<FeeRateProviderInfo> Providers = [
		("BlockstreamInfo", ("https://blockstream.info", "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion"), "/api/fee-estimates", BlockstreamHandler()),
		("MempoolSpace", ("https://mempool.space", "http://mempoolhqx4isw62xs7abwphsq7ldayuidyx2v2oethdhhj6mlo2r6ad.onion/"), "/api/v1/fees/recommended", MempoolSpaceHandler()),

	];

	public async Task<FeeRateEstimations> GetFeeRateEstimationsAsync(string providerName, string userAgent, CancellationToken cancellationToken)
	{
		var providerInfo = Providers.FirstOrDefault(x => x.Name.Equals(providerName, StringComparison.InvariantCultureIgnoreCase));
		if (providerInfo == default)
		{
			throw new NotSupportedException($"Fee rate estimations provider '{providerName}' is not supported.");
		}
		var url = new Uri(httpClientFactory is OnionHttpClientFactory ? providerInfo.ApiDomain.Onion : providerInfo.ApiDomain.ClearNet);

		var httpClient = httpClientFactory.CreateClient($"{providerName}-bitcoin-fee-rate-provider");
		httpClient.BaseAddress = new Uri($"{url.Scheme}://{url.Host}");
		httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);

		using var response = await httpClient.GetAsync(providerInfo.ApiEndPoint, cancellationToken).ConfigureAwait(false);
		var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		var estimations = providerInfo.Extractor(json);
		return estimations;
	}

	private static Func<string, FeeRateEstimations> BlockstreamHandler() =>
		json => new FeeRateEstimations(
			JsonDocument.Parse(json).RootElement
				.EnumerateObject()
				.Select(o => (int.Parse(o.Name), (int) Math.Ceiling(o.Value.GetDouble())))
				.ToDictionary());

	private static Func<string, FeeRateEstimations> MempoolSpaceHandler() =>
		json =>
			new FeeRateEstimations(
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
