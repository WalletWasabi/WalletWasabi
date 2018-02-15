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
		/// <remarks>
		/// Sample request:
		///
		///     GET /fees?2,144,1008
		///
		/// </remarks>
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
		/// <remarks>
		/// Sample request:
		///
		///     POST /broadcast
		///     "483045022100dbd9f153ed42e15284051183a83aa8b4574b680c17085bb94a40fdb8cdcabee00220245a9eda9bab181b336a136b243a45b32216fb16c649eae1e8a58158ecd790a4012102a03b3919772c3a6729a604765a4450df242fe41d8f27d17db722f67afad726f3"
		///
		/// </remarks>
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
		/// <remarks>
		/// Sample request:
		///
		///     GET /filters/00000000000000000044d076d9c43b5888551027ec70043211365301665da2e8
		///
		/// </remarks>
		/// <param name="bestKnownBlockHash">The best block hash the client knows its filter.</param>
		/// <returns>An array of block hash : filter pairs.</returns>
		[HttpGet("filters/{bestKnownBlockHash}")]
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