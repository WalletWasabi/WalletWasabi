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
    [Route("api/v1/btc/[controller]")]
    public class BlockchainController : Controller
	{
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

		[HttpPost("broadcast")]
		public IActionResult Broadcast([FromBody]string hex)
		{
			if(string.IsNullOrWhiteSpace(hex))
			{
				return BadRequest();
			}

			// if fail return BadRequest?

			return Ok();
		}

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

		[HttpGet("filters/{blockHash}")]
		public IActionResult GetFilters(uint256 blockHash)
		{
			if (blockHash == null)
			{
				return BadRequest();
			}

			// if blockHash is not found, return NotFound
			var filters = new List<BlockHashFilterPair>
			{
				new BlockHashFilterPair(){BlockHash = uint256.Zero, FilterHex = "foo"},
				new BlockHashFilterPair(){BlockHash = uint256.One, FilterHex = "bar"},
			};

			return Ok(filters);
		}
	}
}