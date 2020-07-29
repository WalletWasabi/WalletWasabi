using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;

namespace WalletWasabi.WebClients.BlockchainInfo
{
	public partial class CoinstampExchangeRateProvider : IExchangeRateProvider
	{
		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			using var httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri("https://www.bitstamp.net");
			using var response = await httpClient.GetAsync("/api/v2/ticker/btcusd");
			using var content = response.Content;
			var rate = await content.ReadAsJsonAsync<CoinstampExchangeRate>();

			var exchangeRates = new List<ExchangeRate>
			{
				new ExchangeRate { Rate = rate.Rate, Ticker = "USD" }
			};

			return exchangeRates;
		}
	}
}
