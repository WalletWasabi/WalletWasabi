using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Indexer.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Indexer.Controllers;

/// <summary>
/// To acquire offchain data.
/// </summary>
[Produces("application/json")]
[Route("api/v" + Constants.IndexerMajorVersion + "/btc/[controller]")]
public class OffchainController : ControllerBase
{
	public OffchainController(IMemoryCache memoryCache, IExchangeRateProvider exchangeRateProvider)
	{
		_cache = memoryCache;
		_exchangeRateProvider = exchangeRateProvider;
	}

	private readonly IMemoryCache _cache;
	private readonly IExchangeRateProvider _exchangeRateProvider;

	/// <summary>
	/// Gets exchange rates for one Bitcoin.
	/// </summary>
	/// <returns>ExchangeRates[] contains ticker and exchange rate pairs.</returns>
	/// <response code="200">Returns an array of exchange rates.</response>
	/// <response code="404">Exchange rates are not available.</response>
	[HttpGet("exchange-rates")]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	[ResponseCache(Duration = 120)]
	public async Task<IActionResult> GetExchangeRatesAsync(CancellationToken cancellationToken)
	{
		IEnumerable<ExchangeRate> exchangeRates = await GetExchangeRatesCollectionAsync(cancellationToken);

		if (!exchangeRates.Any())
		{
			return NotFound("Exchange rates are not available.");
		}

		return Ok(exchangeRates);
	}

	internal async Task<IEnumerable<ExchangeRate>> GetExchangeRatesCollectionAsync(CancellationToken cancellationToken)
	{
		var cacheKey = nameof(GetExchangeRatesCollectionAsync);

		if (!_cache.TryGetValue(cacheKey, out IEnumerable<ExchangeRate>? exchangeRates))
		{
			exchangeRates = await _exchangeRateProvider.GetExchangeRateAsync(cancellationToken).ConfigureAwait(false);

			if (exchangeRates.Any())
			{
				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(500));

				_cache.Set(cacheKey, exchangeRates, cacheEntryOptions);
			}
		}

		return exchangeRates!;
	}
}
