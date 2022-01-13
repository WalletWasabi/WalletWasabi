using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.WebClients.BlockchainInfo;
using WalletWasabi.WebClients.Coinbase;
using WalletWasabi.WebClients.CoinGecko;
using WalletWasabi.WebClients.Bitstamp;
using WalletWasabi.WebClients.Gemini;
using WalletWasabi.WebClients.ItBit;
using Xunit;
using WalletWasabi.Interfaces;

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
	public async Task CoinGeckoExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new CoinGeckoExchangeRateProvider());

	[Fact]
	public async Task BitstampExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new BitstampExchangeRateProvider());

	[Fact]
	public async Task GeminiExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new GeminiExchangeRateProvider());

	[Fact]
	public async Task ItBitExchangeRateProviderTestsAsync() =>
		await AssertProviderAsync(new ItBitExchangeRateProvider());

	private async Task AssertProviderAsync(IExchangeRateProvider provider)
	{
		IEnumerable<ExchangeRate> rates = await provider.GetExchangeRateAsync();

		var usdRate = Assert.Single(rates, x => x.Ticker == "USD");
		Assert.NotEqual(0.0m, usdRate.Rate);
	}
}
