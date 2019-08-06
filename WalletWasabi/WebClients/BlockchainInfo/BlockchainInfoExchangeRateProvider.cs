using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;

namespace WalletWasabi.WebClients.BlockchainInfo
{
	public class BlockchainInfoExchangeRateProvider : IExchangeRateProvider
	{
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

		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			using (var httpClient = new HttpClient())
			{
				httpClient.BaseAddress = new Uri("https://blockchain.info");
				var response = await httpClient.GetAsync("/ticker");
				var rates = await response.Content.ReadAsJsonAsync<BlockchainInfoExchangeRates>();

				var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate { Rate = rates.USD.Sell, Ticker = "USD" }
				};

				return exchangeRates;
			}
		}
	}
}
