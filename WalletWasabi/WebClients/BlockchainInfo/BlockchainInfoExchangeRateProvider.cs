using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.BlockchainInfo;

public class BlockchainInfoExchangeRateProvider : IExchangeRateProvider
{
	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync()
	{
		using var httpClient = new HttpClient
		{
			BaseAddress = new Uri("https://blockchain.info")
		};
		using var response = await httpClient.GetAsync("/ticker").ConfigureAwait(false);
		using var content = response.Content;
		var rates = await content.ReadAsJsonAsync<BlockchainInfoExchangeRates>().ConfigureAwait(false);

		var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate { Rate = rates.USD.Sell, Ticker = "USD" }
				};

		return exchangeRates;
	}

	private class BlockchainInfoExchangeRate
	{
		public decimal Last { get; set; }
		public decimal Buy { get; set; }
		public decimal Sell { get; set; }
	}

	private class BlockchainInfoExchangeRates
	{
		public BlockchainInfoExchangeRate USD { get; set; }
	}
}
