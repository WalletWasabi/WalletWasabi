using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.WebClients.BlockchainInfo;
using WalletWasabi.WebClients.Coinbase;
using WalletWasabi.WebClients.Bitstamp;
using WalletWasabi.WebClients.Gemini;
using Xunit;
using WalletWasabi.Interfaces;
using System.Threading;

namespace WalletWasabi.Tests.IntegrationTests;

public class ExternalApiTests
{
	[Fact]
	public async Task CoinbaseExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new CoinbaseExchangeRateProvider());

	[Fact]
	public async Task BlockchainInfoExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new BlockchainInfoExchangeRateProvider());

	[Fact]
	public async Task BitstampExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new BitstampExchangeRateProvider());

	[Fact]
	public async Task GeminiExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new GeminiExchangeRateProvider());

	private async Task AssertProviderAsync(IExchangeRateProvider provider)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(3));
		IEnumerable<ExchangeRate> rates = await provider.GetExchangeRateAsync(timeoutCts.Token);

		var usdRate = Assert.Single(rates, x => x.Ticker == "USD");
		Assert.NotEqual(0.0m, usdRate.Rate);
	}
}
