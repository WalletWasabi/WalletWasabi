using Microsoft.AspNetCore.Mvc;
using WalletWasabi.Helpers;

namespace WalletWasabi.Indexer.Controllers;

[Produces("application/json")]
[Route("api/v" + Constants.IndexerMajorVersion + "/[controller]")]
public class WasabiController : ControllerBase
{
	[HttpGet("legaldocuments")]
	[ProducesResponseType(typeof(byte[]), 200)]
	public IActionResult GetLegalDocuments()
	{
		return File("No more legal bullshit", "text/plain");
	}
}
