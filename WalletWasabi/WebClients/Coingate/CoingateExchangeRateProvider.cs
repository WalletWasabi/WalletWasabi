using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;

namespace WalletWasabi.WebClients.Coingate;

public class CoingateExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync(CancellationToken cancellationToken)
	{
		// Only used by the Backend.
#pragma warning disable RS0030 // Do not use banned APIs
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://api.coingate.com")
		};
#pragma warning restore RS0030 // Do not use banned APIs

		var response = await httpClient.GetStringAsync("/v2/rates/merchant/BTC/USD", cancellationToken)
			.ConfigureAwait(false);
		var rate = decimal.Parse(response);

		var exchangeRates = new List<ExchangeRate>
		{
			new ExchangeRate { Rate = rate, Ticker = "USD" }
		};

		return exchangeRates;
	}
}
