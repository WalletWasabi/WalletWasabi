using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.WebClients;
using MagicalCryptoWallet.WebClients.SmartBit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using NBitcoin.RPC;

namespace MagicalCryptoWallet.Backend.Controllers
{
	/// <summary>
	/// To interact with the Bitcoin blockchain.
	/// </summary>
	[Produces("application/json")]
	[Route("api/v1/btc/[controller]")]
	public class BlockchainController : Controller
	{
		private readonly IMemoryCache _cache;
		private readonly IExchangeRateProvider _exchangeRateProvider;

		public BlockchainController(IMemoryCache memoryCache, IExchangeRateProvider exchangeRateProvider)
		{
			_cache = memoryCache;
			_exchangeRateProvider = exchangeRateProvider;
		}

		private static RPCClient RpcClient => Global.RpcClient;

		private static Network Network => Global.Config.Network;

		/// <summary>
		/// Get fees for the requested confirmation targets based on Bitcoin Core's estimatesmartfee output.
		/// </summary>
		/// <remarks>
		/// Sample request:
		///
		///     GET /fees/2,144,1008
		///
		/// </remarks>
		/// <param name="confirmationTargets">Confirmation targets in blocks wit comma separation. (2 - 1008)</param>
		/// <returns>Array of fee estimations for the requested confirmation targets. A fee estimation contains estimation mode (Conservative/Economical) and byte per satoshi pairs.</returns>
		/// <response code="200">Returns array of fee estimations for the requested confirmation targets.</response>
		/// <response code="400">If invalid conformation targets were specified. (2 - 1008 integers)</response>
		[HttpGet("fees/{confirmationTargets}")]
		[ProducesResponseType(200)] // Note: If you add typeof(SortedDictionary<int, FeeEstimationPair>) then swagger UI will visualize incorrectly.
		[ProducesResponseType(400)]
		[ResponseCache(Duration = 60, Location=ResponseCacheLocation.Client)]
		public async Task<IActionResult> GetFeesAsync(string confirmationTargets)
		{
			if(string.IsNullOrWhiteSpace(confirmationTargets) || !ModelState.IsValid)
			{
				return BadRequest($"Invalid {nameof(confirmationTargets)} are specified.");
			}

			var confirmationTargetsInts = new HashSet<int>();
			foreach(var targetParam in confirmationTargets.Split(',', StringSplitOptions.RemoveEmptyEntries))
			{
				if(int.TryParse(targetParam, out var target))
				{
					if(target < 2 || target > 1008)
						return BadRequest("All requested confirmation target must be >=2 AND <= 1008.");

					if(confirmationTargetsInts.Contains(target)) 
						continue;
					confirmationTargetsInts.Add(target);
				}
			}

			var feeEstimations = new SortedDictionary<int, FeeEstimationPair>();

			foreach (int target in confirmationTargetsInts)
			{
				if (Network != Network.RegTest)
				{
					// ToDo: This is the most naive way to implement this.
					// 1. Use the sanity check that under 5 satoshi per bytes should not be displayed.
					// 2. Use the RPCResponse.Blocks output to avoid redundant RPC queries.
					// 3. Implement caching.
					var conservativeResponse = await GetEstimateSmartFeeAsync(target, EstimateSmartFeeMode.Conservative);
					var economicalResponse = await GetEstimateSmartFeeAsync(target, EstimateSmartFeeMode.Economical);
					var conservativeFee = conservativeResponse.FeeRate.FeePerK.Satoshi / 1000;
					var economicalFee = economicalResponse.FeeRate.FeePerK.Satoshi / 1000;

					// Sanity check, some miners don't mine transactions under 5 satoshi/bytes.
					conservativeFee = Math.Max(conservativeFee, 5);
					economicalFee = Math.Max(economicalFee, 5);

					feeEstimations.Add(target, new FeeEstimationPair() { Conservative = conservativeFee, Economical = economicalFee });
				}
				else // RegTest cannot estimate fees, so fill up with dummy data
				{
					feeEstimations.Add(target, new FeeEstimationPair() { Conservative = 6 + target, Economical = 5 + target });
				}
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
		///     "01000000014b6b6fced23fa0d772f83fd849ce2f4e8fa51ea49cc12710ebcdc722d74c87f5000000006a47304402206bf1118e381342d0387e47807c83d2c1e919e2e3792f2673579a9ce87a380db002207e471504f96d7830dc9cbb7442332d747a25dcfd5d1530feea92b8a302aa57f4012102a40230b345856cc18ca1d745e7ea52319a012753b050e24d7be64ca0b978fb3effffffff0235662803000000001976a9146adfacaab3dc7c51b3300c4256b184f95cc48f4288acd0dd0600000000001976a91411ff558b1790b8d57cb25b9c07094591cfd2051c88ac00000000"
		///
		/// </remarks>
		/// <param name="hex">The hex string of the raw transaction.</param>
		/// <returns>200 Ok on successful broadcast or 400 BadRequest on failure.</returns>
		/// <response code="200">If broadcast is successful.</response>
		/// <response code="400">If broadcast fails.</response>
		[HttpPost("broadcast")]
		[ProducesResponseType(200)]
		[ProducesResponseType(400)]
		public async Task<IActionResult> BroadcastAsync([FromBody]string hex)
		{
			if(string.IsNullOrWhiteSpace(hex) || !ModelState.IsValid)
			{
				return BadRequest("Invalid hex.");
			}

			Transaction transaction;
			try
			{
				transaction = new Transaction(hex);
			}
			catch(Exception ex)
			{
				Logger.LogDebug<BlockchainController>(ex);
				return BadRequest("Invalid hex.");
			}

			try
			{

				await RpcClient.SendRawTransactionAsync(transaction);
			}
			catch(RPCException ex) when (ex.Message.Contains("already in block chain", StringComparison.InvariantCultureIgnoreCase))
			{
				return Ok("Transaction is already in the blockchain.");
			}
			catch(RPCException ex)
			{
				Logger.LogDebug<BlockchainController>(ex);
				return BadRequest(ex.Message);
			}

			return Ok("Transaction is successfully broadcasted.");
		}

		/// <summary>
		/// Gets exchange rates for one Bitcoin.
		/// </summary>
		/// <returns>ExchangeRates[] contains ticker and exchange rate pairs.</returns>
		/// <response code="200">Returns an array of exchange rates.</response>
		[HttpGet("exchange-rates")]
		[ProducesResponseType(typeof(IEnumerable<ExchangeRate>), 200)]
		[ResponseCache(Duration = 60, Location=ResponseCacheLocation.Client)]
		public async Task<IEnumerable<ExchangeRate>> GetExchangeRatesAsync()
		{
			List<ExchangeRate> exchangeRates;

			if (!_cache.TryGetValue(nameof(GetExchangeRatesAsync), out exchangeRates))
			{
				exchangeRates = await _exchangeRateProvider.GetExchangeRateAsync();

				if(exchangeRates == null){
					throw new HttpRequestException("BTC/USD exchange rate is not available.");
				}

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(20));

				_cache.Set(nameof(GetExchangeRatesAsync), exchangeRates, cacheEntryOptions);
			}

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
		[ProducesResponseType(200)] // Note: If you add typeof(IList<string>) then swagger UI visualization will be ugly.
		[ProducesResponseType(400)]
		[ProducesResponseType(404)]
		public IActionResult GetFilters(string bestKnownBlockHash)
		{
			if (string.IsNullOrWhiteSpace(bestKnownBlockHash) || !ModelState.IsValid)
			{
				return BadRequest("Invalid block hash provided.");
			}
			
			// if blockHash is not found, return NotFound
			var filters = new List<string>
			{
				"00000000000000000019dfb706e432fa16494338a583af9ca643e4cfcf466af3IamAFilterThereIsNoSeparationBecauseBlockHashIsConstantSize",
				"000000000000000000273f2cafd24f69b72b4b694bb9ab7e4c5df17cf9486b34IamAFilterThereIsNoSeparationBecauseBlockHashIsConstantSize2",
				"0000000000000000005a1ff56e464de63be843f6f335c9a32c478c318c6d084eIamAFilterThereIsNoSeparationBecauseBlockHashIsConstantSize3"
			};

			return Ok(filters);
		}

		private async Task<EstimateSmartFeeResponse> GetEstimateSmartFeeAsync(int target, EstimateSmartFeeMode mode)
		{
			EstimateSmartFeeResponse feeResponse=null;

			var cacheKey = $"{nameof(GetEstimateSmartFeeAsync)}_{target}_{Enum.GetName(typeof(EstimateSmartFeeMode), mode)}";

			if (!_cache.TryGetValue(cacheKey, out feeResponse))
			{
				feeResponse = await RpcClient.EstimateSmartFeeAsync(target, mode);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(20));

				_cache.Set(cacheKey, feeResponse, cacheEntryOptions);
			}

			return feeResponse;
		}
	}
}