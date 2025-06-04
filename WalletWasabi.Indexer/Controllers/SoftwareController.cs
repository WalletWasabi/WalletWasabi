using Microsoft.AspNetCore.Mvc;
using WalletWasabi.Indexer.Models.Responses;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Indexer.Controllers;

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
		IndexerMajorVersion = Constants.IndexerMajorVersion,
		CommitHash = GetCommitHash()
	};

	/// <summary>
	/// Gets the latest versions of the client and indexer.
	/// </summary>
	/// <returns>ClientVersion, IndexerMajorVersion.</returns>
	/// <response code="200">ClientVersion, IndexerMajorVersion.</response>
	[HttpGet("versions")]
	[ProducesResponseType(typeof(VersionsResponse), 200)]
	public VersionsResponse GetVersions()
	{
		return _versionsResponse;
	}

	private static string GetCommitHash() =>
		ReflectionUtils.GetAssemblyMetadata("CommitHash") ?? "";
}
