using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.ExchangeRate;
using WalletWasabi.WebClients;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class ExternalApiTests
{
	[Fact]
	public async Task MempoolSpaceExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync("mempoolspace");

	[Fact]
	public async Task BlockchainInfoExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync("Blockchaininfo");

	[Fact]
	public async Task CoinGeckoExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync("CoinGecko");

	[Fact]
	public async Task GeminiExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync("Gemini");

	private async Task AssertProviderAsync(string providerName)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(3));
		var provider = new ExchangeRateProvider();
		var userAgent = UserAgent.GetNew(Random.Shared.Next());
		var rate = await provider.GetExchangeRateAsync(providerName, userAgent, timeoutCts.Token).ConfigureAwait(false);
		Assert.NotEqual(0m, rate.Rate);
	}
}
