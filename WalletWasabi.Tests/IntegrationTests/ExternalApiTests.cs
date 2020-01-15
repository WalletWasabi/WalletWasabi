using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Logging;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.WebClients.BlockchainInfo;
using WalletWasabi.WebClients.Coinbase;
using WalletWasabi.WebClients.Gemini;
using WalletWasabi.WebClients.ItBit;
using WalletWasabi.WebClients.SmartBit;
using WalletWasabi.WebClients.SmartBit.Models;
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
			using var client = new SmartBitClient(network);
			var rateProvider = new SmartBitExchangeRateProvider(client);
			IEnumerable<ExchangeRate> rates = await rateProvider.GetExchangeRateAsync();

			Assert.Contains("USD", rates.Select(x => x.Ticker));
		}

		[Fact]
		public async Task CoinbaseExchangeRateProviderTestsAsync()
		{
			var client = new CoinbaseExchangeRateProvider();
			IEnumerable<ExchangeRate> rates = await client.GetExchangeRateAsync();

			Assert.Contains("USD", rates.Select(x => x.Ticker));
		}

		[Fact]
		public async Task BlockchainInfoExchangeRateProviderTestsAsync()
		{
			var client = new BlockchainInfoExchangeRateProvider();
			IEnumerable<ExchangeRate> rates = await client.GetExchangeRateAsync();

			Assert.Contains("USD", rates.Select(x => x.Ticker));
		}

		[Fact]
		public async Task GeminiExchangeRateProviderTestsAsync()
		{
			var client = new GeminiExchangeRateProvider();
			IEnumerable<ExchangeRate> rates = await client.GetExchangeRateAsync();

			Assert.Contains("USD", rates.Select(x => x.Ticker));
		}

		[Fact]
		public async Task ItBitExchangeRateProviderTestsAsync()
		{
			var client = new ItBitExchangeRateProvider();
			IEnumerable<ExchangeRate> rates = await client.GetExchangeRateAsync();

			Assert.Contains("USD", rates.Select(x => x.Ticker));
		}
	}
}
