using Microsoft.AspNetCore.Mvc;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Controllers;

/// <summary>
/// To acquire administrative data about the software.
/// </summary>
[Produces("application/json")]
[Route("api/[controller]")]
public class SoftwareController : ControllerBase
{
	private readonly VersionsResponse _versionsResponse = new()
	{
		ClientVersion = Constants.ClientVersion.ToString(3),
		BackendMajorVersion = Constants.BackendMajorVersion,
		Ww1LegalDocumentsVersion = Constants.Ww1LegalDocumentsVersion.ToString(),
		Ww2LegalDocumentsVersion = Constants.Ww2LegalDocumentsVersion.ToString(),
		CommitHash = GetCommitHash()
	};

	/// <summary>
	/// Gets the latest versions of the client and backend.
	/// </summary>
	/// <returns>ClientVersion, BackendMajorVersion.</returns>
	/// <response code="200">ClientVersion, BackendMajorVersion.</response>
	[HttpGet("versions")]
	[ProducesResponseType(typeof(VersionsResponse), 200)]
	public VersionsResponse GetVersions()
	{
		return _versionsResponse;
	}

	private static string GetCommitHash() =>
		ReflectionUtils.GetAssemblyMetadata("CommitHash") ?? "";
}
