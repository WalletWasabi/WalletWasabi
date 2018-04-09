using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MagicalCryptoWallet;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.WebClients.SmartBit;
using NBitcoin;
using Newtonsoft.Json;

namespace MagicalCryptoWallet.WebClients
{
	public interface IExchangeRateProvider
	{
		Task<List<ExchangeRate>> GetExchangeRateAsync();
	}


	class SmartBitExchangeRateProvider : IExchangeRateProvider
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

	class BlockchainInfoExchangeRateProvider : IExchangeRateProvider
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

	class CoinbaseExchangeRateProvider : IExchangeRateProvider
	{
		class DataWrapper{
			public class CoinbaseExchangeRate{
				public class ExchangeRates{
					public decimal USD { get; set; }
				}
				public ExchangeRates Rates { get; set; }
			}
			public CoinbaseExchangeRate Data {get; set;}
		}
		
		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			using(var httpClient = new HttpClient())
			{
				httpClient.BaseAddress = new Uri("https://api.coinbase.com");
				var response = await httpClient.GetAsync("/v2/exchange-rates?currency=BTC");
				var wrapper = await response.ReadAsAsync<DataWrapper>();

				var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate() { Rate = wrapper.Data.Rates.USD, Ticker = "USD" },
				};

				return exchangeRates;
			}
		}
	}


	public class ItBitExchangeRateProvider : IExchangeRateProvider
	{
		class ItBitExchangeRateInfo{
			public decimal Bid { get; set; }
		}
		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			using(var httpClient = new HttpClient())
			{
				httpClient.BaseAddress = new Uri("https://api.itbit.com");
				var response = await httpClient.GetAsync("v1/markets/XBTUSD/ticker");
				var data = await response.ReadAsAsync<ItBitExchangeRateInfo>();

				var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate() { Rate = data.Bid, Ticker = "USD" },
				};

				return exchangeRates;
			}
		}
	}

	public class GeminiExchangeRateProvider : IExchangeRateProvider
	{
		class GeminiExchangeRateInfo{
			public decimal Bid { get; set; }
		}
		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			using(var httpClient = new HttpClient())
			{
				httpClient.BaseAddress = new Uri("https://api.gemini.com");
				var response = await httpClient.GetAsync("/v1/pubticker/btcusd");
				var data = await response.ReadAsAsync<GeminiExchangeRateInfo>();

				var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate() { Rate = data.Bid, Ticker = "USD" },
				};

				return exchangeRates;
			}
		}
	}

	public class ExchangeRateProvider : IExchangeRateProvider
	{
		private  readonly IExchangeRateProvider[] _exchangeRateProviders = new IExchangeRateProvider[]{
			new SmartBitExchangeRateProvider(new SmartBitClient(Network.Main, disposeHandler: true)),
			new BlockchainInfoExchangeRateProvider(),
			new CoinbaseExchangeRateProvider(),
			new GeminiExchangeRateProvider(),
			new ItBitExchangeRateProvider()
		};

		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			List<ExchangeRate> exchangeRates = null;

			foreach(var provider in _exchangeRateProviders)
			{
				try
				{
					exchangeRates = await provider.GetExchangeRateAsync();
					break;
				}catch(Exception){
					// Ignore it and try with the next one
				}
			}
			return exchangeRates;
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

