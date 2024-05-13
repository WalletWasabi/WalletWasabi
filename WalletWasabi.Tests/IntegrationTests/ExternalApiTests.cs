using System.Threading.Tasks;
using Xunit;
using WalletWasabi.Interfaces;
using System.Threading;

namespace WalletWasabi.Tests.IntegrationTests;

public class ExternalApiTests
{
	[Fact]
	public async Task CoinbaseExchangeRateProviderTestsAsync2() =>
		await AssertProviderAsync("Coinbase");

	[Fact]
	public async Task BlockchainInfoExchangeRateProviderTestsAsync2() =>
		await AssertProviderAsync("Blockchain.info");

	[Fact]
	public async Task CoinGeckoExchangeRateProviderTestsAsync2() =>
		await AssertProviderAsync("CoinGecko");

	[Fact]
	public async Task BitstampExchangeRateProviderTestsAsync2() =>
		await AssertProviderAsync("Bitstamp");

	[Fact]
	public async Task GeminiExchangeRateProviderTestsAsync2() =>
		await AssertProviderAsync("Gemini");

	[Fact]
	public async Task CoingateExchangeRateProviderTestsAsync2() =>
		await AssertProviderAsync("Coingate");

	private async Task AssertProviderAsync(string providerName)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(3));
		var provider = new ExchangeRateProvider();
		var rate = await provider.GetExchangeRateAsync(providerName, timeoutCts.Token).ConfigureAwait(false);
		Assert.NotEqual(0m, rate.Rate);
	}
}
