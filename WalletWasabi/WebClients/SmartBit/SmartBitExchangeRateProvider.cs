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
		public SmartBitExchangeRateProvider(SmartBitClient smartBitClient)
		{
			Client = smartBitClient;
		}

		private SmartBitClient Client { get; }

		public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync()
		{
			var rates = await Client.GetExchangeRatesAsync(CancellationToken.None);
			var rate = rates.Single(x => x.Code == "USD");

			var exchangeRates = new List<ExchangeRate>
			{
				new ExchangeRate { Rate = rate.Rate, Ticker = "USD" }
			};

			return exchangeRates;
		}
	}
}
