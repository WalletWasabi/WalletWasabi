﻿using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Models;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To make batched requests.
	/// </summary>
	[Produces("application/json")]
	[Route("api/v" + Helpers.Constants.BackendMajorVersion + "/btc/[controller]")]
	public class BatchController : Controller
	{
		public BlockchainController BlockchainController { get; }
		public ChaumianCoinJoinController ChaumianCoinJoinController { get; }
		public HomeController HomeController { get; }
		public OffchainController OffchainController { get; }

		public BatchController(BlockchainController blockchainController, ChaumianCoinJoinController chaumianCoinJoinController, HomeController homeController, OffchainController offchainController)
		{
			BlockchainController = blockchainController;
			ChaumianCoinJoinController = chaumianCoinJoinController;
			HomeController = homeController;
			OffchainController = offchainController;
		}

		[HttpGet("synchronize")]
		public async Task<IActionResult> GetSynchronizeAsync([FromQuery]string bestKnownBlockHash, [FromQuery]int maxNumberOfFilters, [FromQuery]string estimateSmartFeeMode)
		{
			if (string.IsNullOrWhiteSpace(bestKnownBlockHash))
			{
				return BadRequest("Invalid block hash is provided.");
			}
			if (maxNumberOfFilters <= 0)
			{
				return BadRequest("Invalid maxNumberOfFilters is provided.");
			}

			bool estimateSmartFee = !string.IsNullOrWhiteSpace(estimateSmartFeeMode);
			EstimateSmartFeeMode mode = EstimateSmartFeeMode.Conservative;
			if (estimateSmartFee)
			{
				if (!Enum.TryParse(estimateSmartFeeMode, ignoreCase: true, out mode))
				{
					return BadRequest("Invalid estimation mode is provided, possible values: ECONOMICAL/CONSERVATIVE.");
				}
			}

			if (!ModelState.IsValid)
			{
				return BadRequest("Wrong body is provided.");
			}

			var knownHash = new uint256(bestKnownBlockHash);

			(Height bestHeight, IEnumerable<string> filters) = Global.IndexBuilderService.GetFilterLinesExcluding(knownHash, maxNumberOfFilters, out bool found);

			var response = new SynchronizeResponse();
			response.Filters = new string[0];
			response.BestHeight = bestHeight;

			if (!found)
			{
				response.FiltersResponseState = FiltersResponseState.BestKnownHashNotFound;
			}
			else if (!filters.Any())
			{
				response.FiltersResponseState = FiltersResponseState.NoNewFilter;
			}
			else
			{
				response.FiltersResponseState = FiltersResponseState.NewFilters;
				response.Filters = filters;
			}

			response.CcjRoundStates = ChaumianCoinJoinController.GetStatesCollection();

			if (estimateSmartFee)
			{
				response.AllFeeEstimate = await BlockchainController.GetAllFeeEstimateAsync(mode);
			}

			response.ExchangeRates = await OffchainController.GetExchangeRatesCollectionAsync();

			return Ok(response);
		}
	}
}
