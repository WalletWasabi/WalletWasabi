using System.Threading;
using System.Threading.Tasks;
using Xunit;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.Wallets.Exchange;

namespace WalletWasabi.Tests.IntegrationTests;

public class ExternalApiTests
{
	[Fact]
	public async Task MempoolSpaceExchangeRateProviderTestsAsync() =>
		await AssertExchangeRateProviderAsync(ExchangeRateProviders.MempoolSpaceAsync);

	[Fact]
	public async Task BlockchainInfoExchangeRateProviderTestsAsync() =>
		await AssertExchangeRateProviderAsync(ExchangeRateProviders.BlockstreamAsync);

	[Fact]
	public async Task CoinGeckoExchangeRateProviderTestsAsync() =>
		await AssertExchangeRateProviderAsync(ExchangeRateProviders.CoinGeckoAsync);

	[Fact]
	public async Task GeminiExchangeRateProviderTestsAsync() =>
		await AssertExchangeRateProviderAsync(ExchangeRateProviders.GeminiAsync);

	private async Task AssertExchangeRateProviderAsync(Func<HttpClientFactory, ExchangeRateProvider> providerFactory)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(3));
		var provider = providerFactory(new HttpClientFactory());
		var exchangeRate = await provider(CancellationToken.None).ConfigureAwait(false);
		Assert.True(exchangeRate.Rate > 0);
	}

	[Fact]
	public async Task BlockstreamFeeRateProviderTestsAsync() =>
		await AssertFeeProviderAsync(FeeRateProviders.BlockstreamAsync);

	[Fact]
	public async Task MempoolSpaceRateProviderTestsAsync() =>
		await AssertFeeProviderAsync(FeeRateProviders.MempoolSpaceAsync);

	private async Task AssertFeeProviderAsync(Func<HttpClientFactory, FeeRateProvider> providerFactory)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(3));
		var provider = providerFactory(new HttpClientFactory());
		var estimations = await provider(CancellationToken.None).ConfigureAwait(false);
		Assert.NotEmpty(estimations.Estimations);
	}
}
