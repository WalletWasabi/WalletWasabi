using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagicalCryptoWallet.Backend.Models;
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
		/// <remarks>
		/// Sample request:
		///
		///     GET /fees?2,144,1008
		///
		/// </remarks>
		/// <param name="confirmationTargets">Confirmation target in blocks. (2 - 1008)</param>
		/// <returns>Array of fee estimations for the requested confirmation targets. A fee estimation contains estimation mode (Conservative/Economical) and byte per satoshi pairs.</returns>
		/// <response code="200">Returns array of fee estimations for the requested confirmation targets.</response>
		/// <response code="400">If invalid conformation targets were specified. (2 - 1008 integers)</response>
		[HttpGet("fees")]
		[ProducesResponseType(200)] // Note: If you add typeof(SortedDictionary<int, FeeEstimationPair>) then swagger UI will visualize incorrectly.
		[ProducesResponseType(400)]
		[Produces("application/json")]
		public IActionResult GetFees(IEnumerable<int> confirmationTargets) // ToDo: make it comma separated, currently: ?confirmationTargets=1&confirmationTargets=2
		{
			// Default answer without spefifying confirmationTargets
			if(confirmationTargets == null || confirmationTargets.Count() == 0)
			{
				confirmationTargets = new List<int> { 2, 144, 1008 };
			}

			if (confirmationTargets.Any(x => x < 2 || x > 1008) || !ModelState.IsValid)
			{
				return BadRequest("All requested confirmation target must be >=2 AND <= 1008.");
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
		/// <remarks>
		/// Sample request:
		///
		///     POST /broadcast
		///     "483045022100dbd9f153ed42e15284051183a83aa8b4574b680c17085bb94a40fdb8cdcabee00220245a9eda9bab181b336a136b243a45b32216fb16c649eae1e8a58158ecd790a4012102a03b3919772c3a6729a604765a4450df242fe41d8f27d17db722f67afad726f3"
		///
		/// </remarks>
		/// <param name="hex">The hex string of the raw transaction.</param>
		/// <returns>200 Ok on successful broadcast or 400 BadRequest on failure.</returns>
		/// <response code="200">If broadcast is successful.</response>
		/// <response code="400">If broadcast fails.</response>
		[HttpPost("broadcast")]
		[ProducesResponseType(200)]
		[ProducesResponseType(400)]
		[Produces("application/json")]
		public IActionResult Broadcast([FromBody]string hex)
		{
			if(string.IsNullOrWhiteSpace(hex) || !ModelState.IsValid)
			{
				return BadRequest("Invalid hex.");
			}

			// ToDo: If fail return BadRequest with the RPC error details.

			return Ok("Transaction is successfully broadcasted.");
		}

		/// <summary>
		/// Gets exchange rates for one Bitcoin.
		/// </summary>
		/// <returns>ExchangeRates[] contains ticker and exchange rate pairs.</returns>
		/// <response code="200">Returns an array of exchange rates.</response>
		[HttpGet("exchange-rates")]
		[ProducesResponseType(typeof(IEnumerable<ExchangeRate>), 200)]
		[Produces("application/json")]
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
		/// <remarks>
		/// Sample request:
		///
		///     GET /filters/00000000000000000044d076d9c43b5888551027ec70043211365301665da2e8
		///
		/// </remarks>
		/// <param name="bestKnownBlockHash">The best block hash the client knows its filter.</param>
		/// <returns>An array of block hash : filter pairs.</returns>
		/// <response code="200">An array of block hash and filter pairs.</response>
		/// <response code="400">The provided hash was malformed.</response>
		/// <response code="404">If the hash is not found. This happens at blockhain reorg.</response>
		[HttpGet("filters/{bestKnownBlockHash}")]
		[ProducesResponseType(typeof(IList<BlockHashFilterPair>), 200)] // ToDo: make reutn type more compact, eg: {hash}{filter}\n
		[ProducesResponseType(400)]
		[ProducesResponseType(404)]
		public IActionResult GetFilters(string bestKnownBlockHash)
		{
			if (string.IsNullOrWhiteSpace(bestKnownBlockHash) || !ModelState.IsValid)
			{
				return BadRequest("Invalid block hash provided.");
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