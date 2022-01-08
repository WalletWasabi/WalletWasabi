using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.ItBit;

public class ItBitExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync()
	{
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://api.itbit.com")
		};
		using var response = await httpClient.GetAsync("v1/markets/XBTUSD/ticker").ConfigureAwait(false);
		using var content = response.Content;
		var data = await content.ReadAsJsonAsync<ItBitExchangeRateInfo>().ConfigureAwait(false);

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
