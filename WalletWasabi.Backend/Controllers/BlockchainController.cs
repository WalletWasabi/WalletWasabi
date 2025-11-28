using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Helpers;

namespace WalletWasabi.Backend.Controllers;

/// <summary>
/// To interact with the Bitcoin Blockchain.
/// </summary>
[Produces("application/json")]
[Route("api/v" + Constants.BackendMajorVersion + "/btc/[controller]")]
public class BlockchainController : ControllerBase
{
	public BlockchainController(IndexBuilderService indexBuilderService)
	{
		IndexBuilderService = indexBuilderService;
	}

	private IndexBuilderService IndexBuilderService { get; }


	/// <summary>
	/// Gets block filters from the provided block hash.
	/// </summary>
	/// <remarks>
	/// Filter examples:
	///
	///     Main: 0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893
	///     TestNet: 00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a
	///     RegTest: 0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206
	///
	/// </remarks>
	/// <param name="bestKnownBlockHash">The best block hash the client knows its filter.</param>
	/// <param name="count">The number of filters to return.</param>
	/// <returns>The best height and an array of block hash : element count : filter pairs.</returns>
	/// <response code="200">The best height and an array of block hash : element count : filter pairs.</response>
	/// <response code="204">When the provided hash is the tip.</response>
	/// <response code="400">The provided hash was malformed or the count value is out of range</response>
	/// <response code="404">If the hash is not found. This happens at blockchain reorg.</response>
	[HttpGet("filters")]
	[ProducesResponseType(200)] // Note: If you add typeof(IList<string>) then swagger UI visualization will be ugly.
	[ProducesResponseType(204)]
	[ProducesResponseType(400)]
	[ProducesResponseType(404)]
	[ResponseCache(Duration = 60)]
	public async Task<IActionResult> GetFilters([FromQuery, Required] string bestKnownBlockHash, [FromQuery, Required] int count, CancellationToken cancellationToken)
	{
		if (count <= 0)
		{
			return BadRequest("Invalid block hash or count is provided.");
		}

		var knownHash = new uint256(bestKnownBlockHash);

		var (bestHeight, filters, found) = await IndexBuilderService.GetFilterLinesExcludingAsync(knownHash, count, cancellationToken);

		if (!found)
		{
			return NotFound($"Provided {nameof(bestKnownBlockHash)} is not found: {bestKnownBlockHash}.");
		}

		if (!filters.Any())
		{
			return NoContent();
		}

		var response = new FiltersResponse
		{
			BestHeight = bestHeight,
			Filters = filters
		};

		return Ok(response);
	}
}
