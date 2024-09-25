using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Extensions;
using WalletWasabi.Interfaces;

namespace WalletWasabi.WebClients.Bitstamp;

public class BitstampExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync(CancellationToken cancellationToken)
	{
		// Only used by the Backend.
#pragma warning disable RS0030 // Do not use banned APIs
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://www.bitstamp.net")
		};
#pragma warning restore RS0030 // Do not use banned APIs

		using var response = await httpClient.GetAsync("api/v2/ticker/btcusd", cancellationToken).ConfigureAwait(false);
		using var content = response.Content;
		var rate = await content.ReadAsJsonAsync<BitstampExchangeRate>().ConfigureAwait(false);

		var exchangeRates = new List<ExchangeRate>
			{
				new ExchangeRate { Rate = rate.Rate, Ticker = "USD" }
			};

		return exchangeRates;
	}
}
