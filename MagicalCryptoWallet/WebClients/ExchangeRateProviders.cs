using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MagicalCryptoWallet;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.WebClients.SmartBit;
using Newtonsoft.Json;

namespace MagicalCryptoWallet.WebClients
{
	public interface IExchangeRateProvider
	{
		Task<List<ExchangeRate>> GetExchangeRateAsync();
	}


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

	public class BlockchainInfoExchangeRateProvider : IExchangeRateProvider
	{
		class BlockchainInfoExchangeRate{
			public decimal Last { get; set; }
			public decimal Buy { get; set; }
			public decimal Sell { get; set; }
		}

		class BlockchainInfoExchangeRates{
			public BlockchainInfoExchangeRate USD { get; set; }
		}

		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			using(var httpClient = new HttpClient())
			{
				httpClient.BaseAddress = new Uri("https://blockchain.info");
				var response = await httpClient.GetAsync("/ticker");
				var rates = await response.ReadAsAsync<BlockchainInfoExchangeRates>();

				var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate() { Rate = rates.USD.Sell, Ticker = "USD" },
				};

				return exchangeRates;
			}
		}
	}

	static class HttpResponseMessageExtensions
	{
		public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage me)
		{
			var jsonString =  await me.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<T>(jsonString);
		}
	}	
}

