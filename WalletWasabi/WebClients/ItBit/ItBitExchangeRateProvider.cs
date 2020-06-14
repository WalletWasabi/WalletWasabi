using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;

namespace WalletWasabi.WebClients.ItBit
{
	public class ItBitExchangeRateProvider : IExchangeRateProvider
	{
		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			using var httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri("https://api.itbit.com");
			using var response = await httpClient.GetAsync("v1/markets/XBTUSD/ticker");
			using var content = response.Content;
			var data = await content.ReadAsJsonAsync<ItBitExchangeRateInfo>();

			var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate { Rate = data.Bid, Ticker = "USD" }
				};

			return exchangeRates;
		}

		private class ItBitExchangeRateInfo
		{
			public decimal Bid { get; set; }
		}
	}
}
