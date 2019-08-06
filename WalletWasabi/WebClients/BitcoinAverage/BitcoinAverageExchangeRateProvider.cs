using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;

namespace WalletWasabi.WebClients.BitcoinAverage
{
	public class BitcoinAverageExchangeRateProvider : IExchangeRateProvider
	{
		private class BitcoinAverageExchangeRate
		{
			public decimal Price { get; set; }
		}

		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			using (var httpClient = new HttpClient())
			{
				httpClient.BaseAddress = new Uri("https://apiv2.bitcoinaverage.com");
				var response = await httpClient.GetAsync("/convert/global?from=BTC&to=USD&amount=1");
				var rate = await response.Content.ReadAsJsonAsync<BitcoinAverageExchangeRate>();

				var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate { Rate = rate.Price, Ticker = "USD" }
				};

				return exchangeRates;
			}
		}
	}
}
