using NBitcoin;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.WebClients.BlockchainInfo;
using WalletWasabi.WebClients.Coinbase;
using WalletWasabi.WebClients.CoinGecko;
using WalletWasabi.WebClients.Bitstamp;
using WalletWasabi.WebClients.Gemini;
using WalletWasabi.WebClients.ItBit;
using WalletWasabi.WebClients.SmartBit;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	public class ExternalApiTests
	{
		[Theory]
		[InlineData("test")]
		[InlineData("main")]
		public async Task SmartBitExchangeRateProviderTestAsync(string networkString)
		{
			var network = Network.GetNetwork(networkString);
			var client = new SmartBitClient(network);
			var rateProvider = new SmartBitExchangeRateProvider(client);
			IEnumerable<ExchangeRate> rates = await rateProvider.GetExchangeRateAsync();

			var usdRate = Assert.Single(rates, x => x.Ticker == "USD");
			Assert.NotEqual(0.0m, usdRate.Rate);
		}

		[Fact]
		public async Task CoinbaseExchangeRateProviderTestsAsync()
		{
			var client = new CoinbaseExchangeRateProvider();
			IEnumerable<ExchangeRate> rates = await client.GetExchangeRateAsync();

			var usdRate = Assert.Single(rates, x => x.Ticker == "USD");
			Assert.NotEqual(0.0m, usdRate.Rate);
		}

		[Fact]
		public async Task BlockchainInfoExchangeRateProviderTestsAsync()
		{
			var client = new BlockchainInfoExchangeRateProvider();
			IEnumerable<ExchangeRate> rates = await client.GetExchangeRateAsync();

			var usdRate = Assert.Single(rates, x => x.Ticker == "USD");
			Assert.NotEqual(0.0m, usdRate.Rate);
		}

		[Fact]
		public async Task CoinGeckoExchangeRateProviderTestsAsync()
		{
			var client = new CoinGeckoExchangeRateProvider();
			IEnumerable<ExchangeRate> rates = await client.GetExchangeRateAsync();

			var usdRate = Assert.Single(rates, x => x.Ticker == "USD");
			Assert.NotEqual(0.0m, usdRate.Rate);
		}

		[Fact]
		public async Task BitstampExchangeRateProviderTestsAsync()
		{
			var client = new BitstampExchangeRateProvider();
			IEnumerable<ExchangeRate> rates = await client.GetExchangeRateAsync();

			var usdRate = Assert.Single(rates, x => x.Ticker == "USD");
			Assert.NotEqual(0.0m, usdRate.Rate);
		}

		[Fact]
		public async Task GeminiExchangeRateProviderTestsAsync()
		{
			var client = new GeminiExchangeRateProvider();
			IEnumerable<ExchangeRate> rates = await client.GetExchangeRateAsync();

			var usdRate = Assert.Single(rates, x => x.Ticker == "USD");
			Assert.NotEqual(0.0m, usdRate.Rate);
		}

		[Fact]
		public async Task ItBitExchangeRateProviderTestsAsync()
		{
			var client = new ItBitExchangeRateProvider();
			IEnumerable<ExchangeRate> rates = await client.GetExchangeRateAsync();

			var usdRate = Assert.Single(rates, x => x.Ticker == "USD");
			Assert.NotEqual(0.0m, usdRate.Rate);
		}
	}
}
