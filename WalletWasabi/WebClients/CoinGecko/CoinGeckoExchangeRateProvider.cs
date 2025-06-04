using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Indexer.Models;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Serialization;

namespace WalletWasabi.WebClients.CoinGecko;

public class CoinGeckoExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync(CancellationToken cancellationToken)
	{
		// Only used by the Indexer.
#pragma warning disable RS0030 // Do not use banned APIs
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://api.coingecko.com")
		};
#pragma warning restore RS0030 // Do not use banned APIs

		httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WasabiWallet", Constants.ClientVersion.ToString()));
		using var response = await httpClient.GetAsync("api/v3/coins/markets?vs_currency=usd&ids=bitcoin", cancellationToken).ConfigureAwait(false);
		using var content = response.Content;
		var rates = await content.ReadAsJsonAsync(Decode.Array(Decode.CoinGeckoExchangeRate)).ConfigureAwait(false);

		return rates.Take(1);
	}
}
