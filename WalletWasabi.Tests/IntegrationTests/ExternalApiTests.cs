using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Client.Wallets.Exchange;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class ExternalApiTests
{
	[Fact(Timeout = 180_000)]
	public async Task MempoolSpaceExchangeRateProviderTestsAsync() =>
		await AssertExchangeRateProviderAsync(ExchangeRateProviders.MempoolSpaceAsync, TestContext.Current.CancellationToken);

	[Fact(Timeout = 180_000)]
	public async Task BlockchainInfoExchangeRateProviderTestsAsync() =>
		await AssertExchangeRateProviderAsync(ExchangeRateProviders.BlockchainInfoAsync, TestContext.Current.CancellationToken);

	[Fact(Timeout = 180_000)]
	public async Task CoinGeckoExchangeRateProviderTestsAsync() =>
		await AssertExchangeRateProviderAsync(ExchangeRateProviders.CoinGeckoAsync, TestContext.Current.CancellationToken);

	[Fact(Timeout = 180_000)]
	public async Task GeminiExchangeRateProviderTestsAsync() =>
		await AssertExchangeRateProviderAsync(ExchangeRateProviders.GeminiAsync, TestContext.Current.CancellationToken);

	private async Task AssertExchangeRateProviderAsync(Func<ExchangeRateHttpFetcher, ExchangeRateProvider> providerFactory, CancellationToken cancellationToken)
	{
		var httpFetcher = ExchangeRateProviders.FetcherFactory(new HttpClientFactory());
		var provider = providerFactory(httpFetcher);
		var exchangeRates = await provider(cancellationToken).ConfigureAwait(false);
		Assert.All(exchangeRates, x => Assert.True(x.Rate > 0));
	}

	[Fact(Timeout = 180_000)]
	public async Task BlockstreamFeeRateProviderTestsAsync() =>
		await AssertFeeProviderAsync(FeeRateProviders.BlockstreamAsync, TestContext.Current.CancellationToken);

	[Fact(Timeout = 180_000)]
	public async Task MempoolSpaceRateProviderTestsAsync() =>
		await AssertFeeProviderAsync(FeeRateProviders.MempoolSpaceAsync, TestContext.Current.CancellationToken);

	private async Task AssertFeeProviderAsync(Func<IHttpClientFactory, FeeRateProvider> providerFactory, CancellationToken cancellationToken)
	{
		var provider = providerFactory(new HttpClientFactory());
		var estimations = await provider(cancellationToken).ConfigureAwait(false);
		Assert.NotEmpty(estimations.Estimations);
	}
}
