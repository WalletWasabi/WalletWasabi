using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Backend.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;

namespace MagicalCryptoWallet.Backend.Controllers
{
    [Route("api/v1/btc/[controller]")]
    public class BlockchainController : Controller
	{
		[HttpGet("fees")]
		public IDictionary<int, FeeEstimationPair> GetFees(IEnumerable<int> confirmationTargets) // ToDo: make it comma separated, currently: ?confirmationTargets=1&confirmationTargets=2
		{
			var feeEstimations = new SortedDictionary<int, FeeEstimationPair>();
			foreach (var target in confirmationTargets)
			{
				feeEstimations.Add(target, new FeeEstimationPair() { Conservative = 200, Economical = 199 });
			}

			return feeEstimations;
		}

		[HttpPost("broadcast")]
		public IActionResult Broadcast([FromBody]string hex)
		{
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
		public IEnumerable<BlockHashFilterPair> GetFilters(uint256 blockHash)
		{
			var filters = new List<BlockHashFilterPair>
			{
				new BlockHashFilterPair(){BlockHash = uint256.Zero, FilterHex = "foo"},
				new BlockHashFilterPair(){BlockHash = uint256.One, FilterHex = "bar"},
			};

			return filters;
		}
	}
}