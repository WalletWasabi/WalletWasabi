using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;

namespace WalletWasabi.WebClients.Coinbase
{
	public class CoinbaseExchangeRateProvider : IExchangeRateProvider
	{
		private class DataWrapper
		{
			public class CoinbaseExchangeRate
			{
				public class ExchangeRates
				{
					public decimal USD { get; set; }
				}

				public ExchangeRates Rates { get; set; }
			}

			public CoinbaseExchangeRate Data { get; set; }
		}

		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			using var httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri("https://api.coinbase.com");
			var response = await httpClient.GetAsync("/v2/exchange-rates?currency=BTC");
			var wrapper = await response.Content.ReadAsJsonAsync<DataWrapper>();

			var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate { Rate = wrapper.Data.Rates.USD, Ticker = "USD" }
				};

			return exchangeRates;
		}
	}
}
