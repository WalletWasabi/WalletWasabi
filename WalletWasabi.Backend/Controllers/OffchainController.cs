using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To acquire offchain data.
	/// </summary>
	[Produces("application/json")]
	[Route("api/v" + Constants.BackendMajorVersion + "/btc/[controller]")]
	public class OffchainController : ControllerBase
	{
		public OffchainController(IMemoryCache memoryCache, IExchangeRateProvider exchangeRateProvider)
		{
			Cache = memoryCache;
			ExchangeRateProvider = exchangeRateProvider;
		}

		private IMemoryCache Cache { get; }
		private IExchangeRateProvider ExchangeRateProvider { get; }

		/// <summary>
		/// Gets exchange rates for one Bitcoin.
		/// </summary>
		/// <returns>ExchangeRates[] contains ticker and exchange rate pairs.</returns>
		/// <response code="200">Returns an array of exchange rates.</response>
		/// <response code="404">Exchange rates are not available.</response>
		[HttpGet("exchange-rates")]
		[ProducesResponseType(200)]
		[ProducesResponseType(404)]
		public async Task<IActionResult> GetExchangeRatesAsync()
		{
			IEnumerable<ExchangeRate> exchangeRates = await GetExchangeRatesCollectionAsync();

			if (!exchangeRates.Any())
			{
				return NotFound("Exchange rates are not available.");
			}

			return Ok(exchangeRates);
		}

		internal async Task<IEnumerable<ExchangeRate>> GetExchangeRatesCollectionAsync()
		{
			var cacheKey = nameof(GetExchangeRatesCollectionAsync);

			if (!Cache.TryGetValue(cacheKey, out IEnumerable<ExchangeRate> exchangeRates))
			{
				exchangeRates = await ExchangeRateProvider.GetExchangeRateAsync();

				if (exchangeRates.Any())
				{
					var cacheEntryOptions = new MemoryCacheEntryOptions()
						.SetAbsoluteExpiration(TimeSpan.FromSeconds(500));

					Cache.Set(cacheKey, exchangeRates, cacheEntryOptions);
				}
			}
			return exchangeRates;
		}
	}
}
