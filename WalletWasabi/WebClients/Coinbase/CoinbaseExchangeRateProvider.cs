using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.Coinbase;

public class CoinbaseExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync()
	{
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://api.coinbase.com")
		};
		using var response = await httpClient.GetAsync("/v2/exchange-rates?currency=BTC").ConfigureAwait(false);
		using var content = response.Content;
		var wrapper = await content.ReadAsJsonAsync<DataWrapper>().ConfigureAwait(false);

		var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate { Rate = wrapper.Data.Rates.USD, Ticker = "USD" }
				};

		return exchangeRates;
	}

	private class DataWrapper
	{
		public CoinbaseExchangeRate Data { get; set; }

		public class CoinbaseExchangeRate
		{
			public ExchangeRates Rates { get; set; }

			public class ExchangeRates
			{
				public decimal USD { get; set; }
			}
		}
	}
}
