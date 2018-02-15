using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Backend.Models.Responses;
using MagicalCryptoWallet.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;

namespace MagicalCryptoWallet.Backend.Controllers
{
	/// <summary>
	/// To interact with the Bitcoin blockchain.
	/// </summary>
    [Route("api/v1/btc/[controller]")]
    public class BlockchainController : Controller
	{
		/// <summary>
		/// Get fees for the requested confirmation targets based on Bitcoin Core's estimatesmartfee output.
		/// </summary>
		/// <param name="confirmationTargets">Confirmation target in blocks. (2 - 1008)</param>
		/// <returns>ConfirmationTarget[] contains estimation mode and byte per satoshi pairs. Example: </returns>
		[HttpGet("fees")]
		public IActionResult GetFees(IEnumerable<int> confirmationTargets) // ToDo: make it comma separated, currently: ?confirmationTargets=1&confirmationTargets=2
		{
			if (confirmationTargets == null || confirmationTargets.Count() == 0 || confirmationTargets.Any(x => x < 2 || x > 1008))
			{
				return BadRequest();
			}
			
			var feeEstimations = new SortedDictionary<int, FeeEstimationPair>();
			foreach (var target in confirmationTargets)
			{
				feeEstimations.Add(target, new FeeEstimationPair() { Conservative = 200, Economical = 199 });
			}

			return Ok(feeEstimations);
		}

		/// <summary>
		/// Attempts to broadcast a transaction.
		/// </summary>
		/// <param name="hex">The hex string of the raw transaction.</param>
		/// <returns>200 Ok</returns>
		[HttpPost("broadcast")]
		public IActionResult Broadcast([FromBody]string hex)
		{
			if(string.IsNullOrWhiteSpace(hex))
			{
				return BadRequest();
			}

			// ToDo: if fail return BadRequest?

			return Ok();
		}

		/// <summary>
		/// Gets exchange rates for one Bitcoin.
		/// </summary>
		/// <returns>ExchangeRates[] contains ticker and exchange rate pairs.</returns>
		[HttpGet("exchange-rates")]
		public IEnumerable<ExchangeRate> GetExchangeRates()
		{
			var exchangeRates = new List<ExchangeRate>
			{
				new ExchangeRate() { Rate = 10000m, Ticker = "USD" },
				new ExchangeRate() { Rate = 7777.123m, Ticker = "CNY" }
			};

			return exchangeRates;
		}

		/// <summary>
		/// Gets block filters from the specified block hash.
		/// </summary>
		/// <param name="bestKnownBlockHash">The best block hash the client knows its filter.</param>
		/// <returns>An array of block hash : filter pairs.</returns>
		[HttpGet("filters/{blockHash}")]
		public IActionResult GetFilters(string bestKnownBlockHash)
		{
			if (string.IsNullOrWhiteSpace(bestKnownBlockHash))
			{
				return BadRequest();
			}

			// if blockHash is not found, return NotFound
			var filters = new List<BlockHashFilterPair>
			{
				new BlockHashFilterPair(){BlockHash = "asdasdasd", FilterHex = "foo"},
				new BlockHashFilterPair(){BlockHash = "asdsadasdad", FilterHex = "bar"},
			};

			return Ok(filters);
		}
	}
}