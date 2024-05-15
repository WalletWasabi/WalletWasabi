using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.ExchangeRate;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class ExternalApiTests
{
	[Fact]
	public async Task CoinbaseExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync("Coinbase");

	[Fact]
	public async Task BlockchainInfoExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync("Blockchain");

	[Fact]
	public async Task CoinGeckoExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync("CoinGecko");

	[Fact]
	public async Task BitstampExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync("Bitstamp");

	[Fact]
	public async Task GeminiExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync("Gemini");

	[Fact]
	public async Task CoingateExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync("Coingate");

	private async Task AssertProviderAsync(string providerName)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(3));
		var provider = new ExchangeRateProvider();
		var rate = await provider.GetExchangeRateAsync(providerName, timeoutCts.Token).ConfigureAwait(false);
		Assert.NotEqual(0m, rate.Rate);
	}
}
