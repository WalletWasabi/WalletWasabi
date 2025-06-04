using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Indexer.Controllers;

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
	public async Task<IActionResult> GetAllFeesV3Async([FromQuery, Required] string estimateSmartFeeMode, CancellationToken cancellationToken)
		=> await BlockchainController.GetAllFeesAsync(estimateSmartFeeMode, cancellationToken);

	[HttpGet("api/v3/btc/Offchain/exchange-rates")]
	public async Task<IActionResult> GetExchangeRatesV3Async(CancellationToken cancellationToken)
		=> await OffchainController.GetExchangeRatesAsync(cancellationToken);
}
