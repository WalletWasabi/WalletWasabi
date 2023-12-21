using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.Coinbase;

public class CoinbaseExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync(CancellationToken cancellationToken)
	{
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://api.coinbase.com")
		};
		using var response = await httpClient.GetAsync("/v2/exchange-rates?currency=BTC", cancellationToken).ConfigureAwait(false);
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
		public required CoinbaseExchangeRate Data { get; init; }

		public class CoinbaseExchangeRate
		{
			public required ExchangeRates Rates { get; init; }

			public class ExchangeRates
			{
				public decimal USD { get; init; }
			}
		}
	}
}
