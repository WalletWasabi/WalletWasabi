using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// Provides legacy, never changing API for legacy applications.
	/// </summary>
	[Produces("application/json")]
	public class LegacyController : ControllerBase
	{
		public LegacyController(BlockchainController blockchainController, OffchainController offchainController)
		{
			BlockchainController = blockchainController;
			OffchainController = offchainController;
		}

		public BlockchainController BlockchainController { get; }
		public OffchainController OffchainController { get; }

		[HttpGet("api/v3/btc/Blockchain/all-fees")]
		[ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
		public async Task<IActionResult> GetAllFeesV3Async([FromQuery, Required] string estimateSmartFeeMode)
			=> await BlockchainController.GetAllFeesAsync(estimateSmartFeeMode);

		[HttpGet("api/v3/btc/Offchain/exchange-rates")]
		public async Task<IActionResult> GetExchangeRatesV3Async()
			=> await OffchainController.GetExchangeRatesAsync();
	}
}
