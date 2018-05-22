using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;

namespace WalletWasabi.WebClients.SmartBit
{
	public class SmartBitExchangeRateProvider : IExchangeRateProvider
	{
		private SmartBitClient _client;

		public SmartBitExchangeRateProvider(SmartBitClient smartBitClient)
		{
			_client = smartBitClient;
		}

		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			var rates = await _client.GetExchangeRatesAsync(CancellationToken.None);
			var rate = rates.Single(x => x.Code == "USD");

			var exchangeRates = new List<ExchangeRate>
			{
				new ExchangeRate() { Rate = rate.Rate, Ticker = "USD" },
			};

			return exchangeRates;
		}
	}
}
