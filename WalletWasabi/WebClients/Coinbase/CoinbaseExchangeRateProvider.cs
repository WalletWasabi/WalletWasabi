using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Extensions;
using WalletWasabi.Interfaces;
using WalletWasabi.Serialization;

namespace WalletWasabi.WebClients.Coinbase;

public class CoinbaseExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync(CancellationToken cancellationToken)
	{
		// Only used by the Backend.
#pragma warning disable RS0030 // Do not use banned APIs
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://api.coinbase.com")
		};
#pragma warning restore RS0030 // Do not use banned APIs

		using var response = await httpClient.GetAsync("/v2/exchange-rates?currency=BTC", cancellationToken).ConfigureAwait(false);
		using var content = response.Content;
		var exchangeRate = await content.ReadAsJsonAsync(Decode.CoinbaseExchangeRate).ConfigureAwait(false);

		return [exchangeRate];
	}
}
