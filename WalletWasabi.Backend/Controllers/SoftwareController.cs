using Microsoft.AspNetCore.Mvc;
using WalletWasabi.Backend.Models.Responses;
using System.IO;
using WalletWasabi.Helpers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To acquire administrative data about the software.
	/// </summary>
	[Produces("application/json")]
	[Route("api/[controller]")]
	public class SoftwareController : Controller
	{
		public VersionsResponse VersionsResponse { get; private set; }

		/// <summary>
		/// Gets the latest versions of the client and backend.
		/// </summary>
		/// <returns>ClientVersion, BackendMajorVersion.</returns>
		/// <response code="200">ClientVersion, BackendMajorVersion.</response>
		[HttpGet("versions")]
		[ProducesResponseType(typeof(VersionsResponse), 200)]
		public async Task<IActionResult> GetVersionsAsync()
		{
			if (VersionsResponse is null)
			{
				VersionsResponse = new VersionsResponse()
				{
					ClientVersion = Constants.ClientVersion.ToString(3),
					BackendMajorVersion = Constants.BackendMajorVersion,
					LegalIssuesHash = HashHelpers.GenerateSha256Hash(await System.IO.File.ReadAllBytesAsync("Assets/LegalIssues.txt")),
					PrivacyPolicyHash = HashHelpers.GenerateSha256Hash(await System.IO.File.ReadAllBytesAsync("Assets/PrivacyPolicy.txt")),
					TermsAndConditionsHash = HashHelpers.GenerateSha256Hash(await System.IO.File.ReadAllBytesAsync("Assets/TermsAndConditions.txt"))
				};
			}

			return Ok(VersionsResponse);
		}
	}
}
