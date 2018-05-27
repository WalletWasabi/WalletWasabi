using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.WebClients;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To acquire offchain data.
	/// </summary>
	[Produces("application/json")]
	[Route("api/v1/btc/[controller]")]
	public class OffchainController : Controller
	{
		private IMemoryCache Cache { get; }
		private IExchangeRateProvider ExchangeRateProvider { get; }

		public OffchainController(IMemoryCache memoryCache, IExchangeRateProvider exchangeRateProvider)
		{
			Cache = memoryCache;
			ExchangeRateProvider = exchangeRateProvider;
		}

		/// <summary>
		/// Gets exchange rates for one Bitcoin.
		/// </summary>
		/// <returns>ExchangeRates[] contains ticker and exchange rate pairs.</returns>
		/// <response code="200">Returns an array of exchange rates.</response>
		[HttpGet("exchange-rates")]
		[ProducesResponseType(typeof(IEnumerable<ExchangeRate>), 200)]
		[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
		public async Task<IEnumerable<ExchangeRate>> GetExchangeRatesAsync()
		{
			if (Cache.TryGetValue(nameof(GetExchangeRatesAsync), out List<ExchangeRate> exchangeRates)) 
				return exchangeRates;

			exchangeRates = await ExchangeRateProvider.GetExchangeRateAsync();

			if (exchangeRates == null)
			{
				throw new HttpRequestException("BTC/USD exchange rate is not available.");
			}

			var cacheEntryOptions = new MemoryCacheEntryOptions()
				.SetAbsoluteExpiration(TimeSpan.FromSeconds(20));

			Cache.Set(nameof(GetExchangeRatesAsync), exchangeRates, cacheEntryOptions);

			return exchangeRates;
		}
	}
}
