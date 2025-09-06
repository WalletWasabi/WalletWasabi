using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;
using NBitcoin;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WebClients;

namespace WalletWasabi.FeeRateEstimation;
using ApiDomains = (string ClearNet, string Onion);
using FeeRateExtractor = Func<string, FeeRateEstimations>;

public delegate Task<FeeRateEstimations> FeeRateProvider(CancellationToken cancellationToken);

public static class FeeRateProviders
{
	public static readonly ImmutableArray<string> Providers =
	[
		"BlockstreamInfo",
		"MempoolSpace",
		"None"
	];

	private static UserAgentPicker PickRandomUserAgent = UserAgent.GenerateUserAgentPicker(false);

	public static FeeRateProvider BlockstreamAsync(IHttpClientFactory httpClientFactory) =>
		cancellationToken => GetFeeRateEstimationsAsync("Blockstream",
			("https://blockstream.info", "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion"),
			"/api/fee-estimates",
			httpClientFactory, PickRandomUserAgent(), BlockstreamHandler(), cancellationToken);

	public static FeeRateProvider MempoolSpaceAsync(IHttpClientFactory httpClientFactory) =>
		cancellationToken => GetFeeRateEstimationsAsync("MempoolSpace",
			("https://mempool.space", "http://mempoolhqx4isw62xs7abwphsq7ldayuidyx2v2oethdhhj6mlo2r6ad.onion/"),
			"/api/v1/fees/recommended",
			httpClientFactory, PickRandomUserAgent(), MempoolSpaceHandler(), cancellationToken);

	public static FeeRateProvider NoneAsync() =>
		_ => Task.FromResult(new FeeRateEstimations(new Dictionary<int, FeeRate> ()));

	public static FeeRateProvider RpcAsync(IRPCClient rpcClient) =>
		async cancellationToken =>
		{
			var allEstimations = await rpcClient.EstimateAllFeeAsync(cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"Fetched fee rate from RPC node: {allEstimations.GetFeeRate(confirmationTarget: 2)}.");
			return new FeeRateEstimations(allEstimations.Estimations);
		};

	public static FeeRateProvider Composed(FeeRateProvider[] feeRateProviders) =>
		async cancellationToken =>
		{
			foreach (var provider in feeRateProviders)
			{
				try
				{
					var estimations = await provider(cancellationToken).ConfigureAwait(false);
					if (estimations.Estimations.Any())
					{
						return estimations;
					}
				}
				catch (Exception)
				{
					// ignore. Try the next provider
				}
			}

			throw new InvalidOperationException("All fee rate providers failed to give us fee estimations.");
		};

	private static async Task<FeeRateEstimations> GetFeeRateEstimationsAsync(string providerName, ApiDomains domains, string apiEndPoint, IHttpClientFactory httpClientFactory, string userAgent, FeeRateExtractor extractor, CancellationToken cancellationToken)
	{
		var url = new Uri(httpClientFactory is OnionHttpClientFactory ? domains.Onion : domains.ClearNet);

		var httpClient = httpClientFactory.CreateClient($"{providerName}-bitcoin-fee-rate-provider");
		httpClient.BaseAddress = new Uri($"{url.Scheme}://{url.Host}");
		httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);

		using var response = await httpClient.GetAsync(apiEndPoint, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode($"Error requesting fee rate estimations to '{providerName}'");
		var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		return Result<FeeRateEstimations, Exception>
			.Catch(() => extractor(json))
			.Match(
				estimations =>
				{
					Logger.LogInfo($"Fetched fee rate from {providerName}: {estimations.GetFeeRate(confirmationTarget: 2)}.");
					return estimations;
				},
				ex => throw new InvalidOperationException($"Error parsing fee rate estimations provider response.", ex));
	}

	private static Func<string, FeeRateEstimations> BlockstreamHandler() =>
		json => new FeeRateEstimations(
			JsonDocument.Parse(json).RootElement
				.EnumerateObject()
				.Select(o => (int.Parse(o.Name), new FeeRate(o.Value.GetDecimal())))
				.ToDictionary());

	private static Func<string, FeeRateEstimations> MempoolSpaceHandler() =>
		json =>
			new FeeRateEstimations(
				JsonDocument.Parse(json).RootElement
					.EnumerateObject()
					.Select(o => (o.Name, o.Value.GetDecimal()) switch {
							("fastestFee", var feeRate) => (Target: 2, feeRate),
							("halfHourFee", var feeRate) => (Target: 3, feeRate),
							("hourFee", var feeRate) => (Target: 6, feeRate),
							("economyFee", var feeRate) => (Target: 72, feeRate),
							_ => (0, 0)
						})
					.Where(x => x.Target > 0)
					.Select(o => (o.Target, new FeeRate(o.feeRate)))
					.ToDictionary());
}
