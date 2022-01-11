using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Legal;

namespace WalletWasabi.Backend.Controllers;

/// <summary>
/// To acquire Wasabi software related data.
/// </summary>
[Produces("application/json")]
[Route("api/v" + Constants.BackendMajorVersion + "/[controller]")]
public class WasabiController : ControllerBase
{
	/// <summary>
	/// Gets the latest legal documents.
	/// </summary>
	/// <returns>Returns the legal documents.</returns>
	/// <response code="200">Returns the legal documents.</response>
	[HttpGet("legaldocuments")]
	[ProducesResponseType(typeof(byte[]), 200)]
	public async Task<IActionResult> GetLegalDocumentsAsync()
	{
		var content = await System.IO.File.ReadAllBytesAsync(LegalDocuments.EmbeddedFilePath);
		return File(content, "text/plain");
	}
}
