using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.Gemini
{
	public class GeminiExchangeRateProvider : IExchangeRateProvider
	{
		public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync()
		{
			using var httpClient = new HttpClient
			{
				BaseAddress = new Uri("https://api.gemini.com")
			};
			using var response = await httpClient.GetAsync("/v1/pubticker/btcusd").ConfigureAwait(false);
			using var content = response.Content;
			var data = await content.ReadAsJsonAsync<GeminiExchangeRateInfo>().ConfigureAwait(false);

			var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate { Rate = data.Bid, Ticker = "USD" }
				};

			return exchangeRates;
		}

		private class GeminiExchangeRateInfo
		{
			public decimal Bid { get; set; }
		}
	}
}
