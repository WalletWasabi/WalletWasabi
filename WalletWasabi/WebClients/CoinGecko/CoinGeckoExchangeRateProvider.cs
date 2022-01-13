using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.CoinGecko;

public class CoinGeckoExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync()
	{
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://api.coingecko.com")
		};
		using var response = await httpClient.GetAsync("/api/v3/coins/markets?vs_currency=usd&ids=bitcoin").ConfigureAwait(false);
		using var content = response.Content;
		var rates = await content.ReadAsJsonAsync<CoinGeckoExchangeRate[]>().ConfigureAwait(false);

		var exchangeRates = new List<ExchangeRate>
			{
				new ExchangeRate { Rate = rates[0].Rate, Ticker = "USD" }
			};

		return exchangeRates;
	}
}
