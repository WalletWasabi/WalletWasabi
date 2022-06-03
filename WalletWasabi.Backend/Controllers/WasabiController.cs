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
	public async Task<IActionResult> GetLegalDocumentsAsync(string? id)
	{
		string filePath;

		// If the document id is null, then the request comes from WW1.0 client.
		if (id is null)
		{
			filePath = LegalDocuments.EmbeddedFilePathForWw1;
		}
		else
		{
			if (Version.TryParse(id, out var requestedVersion) && requestedVersion == Constants.Ww2LegalDocumentsVersion)
			{
				filePath = LegalDocuments.EmbeddedFilePathForWw2;
			}
			else
			{
				return NotFound();
			}
		}

		var content = await System.IO.File.ReadAllBytesAsync(filePath);
		return File(content, "text/plain");
	}
}
