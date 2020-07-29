using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;

namespace WalletWasabi.WebClients.BlockchainInfo
{
	public class CoinGeckoExchangeRateProvider : IExchangeRateProvider
	{
		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			using var httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri("https://api.coingecko.com");
			using var response = await httpClient.GetAsync("/api/v3/coins/markets?vs_currency=usd&ids=bitcoin");
			using var content = response.Content;
			var rates = await content.ReadAsJsonAsync<CoinGeckoExchangeRate[]>();

			var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate { Rate = rates[0].Rate, Ticker = "USD" }
				};

			return exchangeRates;
		}

		public class CoinGeckoExchangeRate
		{

			[JsonProperty(PropertyName = "current_price")]
			public decimal Rate { get; set; }
		}
	}
}
