using Microsoft.AspNetCore.Mvc;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;
using System.Threading.Tasks;
using System.Text;

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
		private const string LegalIssuesPath = "Assets/LegalIssues.txt";
		private const string PrivacyPolicyPath = "Assets/PrivacyPolicy.txt";
		private const string TermsAndConditionsPath = "Assets/TermsAndConditions.txt";

		/// <summary>
		/// Gets the latest versions of the client, backend and legal documents.
		/// </summary>
		/// <returns>ClientVersion, BackendMajorVersion, LegalIssuesHash, PrivacyPolicyHash, TermsAndConditionsHash.</returns>
		/// <response code="200">ClientVersion, BackendMajorVersion, LegalIssuesHash, PrivacyPolicyHash, TermsAndConditionsHash.</response>
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
					LegalIssuesHash = HashHelpers.GenerateSha256Hash(await System.IO.File.ReadAllBytesAsync(LegalIssuesPath)),
					PrivacyPolicyHash = HashHelpers.GenerateSha256Hash(await System.IO.File.ReadAllBytesAsync(PrivacyPolicyPath)),
					TermsAndConditionsHash = HashHelpers.GenerateSha256Hash(await System.IO.File.ReadAllBytesAsync(TermsAndConditionsPath))
				};
			}

			return Ok(VersionsResponse);
		}

		/// <summary>
		/// Gets the latest version of the LegalIssues document.
		/// </summary>
		/// <returns>LegalIssues text document bytes in BASE64 format.</returns>
		/// <response code="200">LegalIssues</response>
		[HttpGet("legalissues")]
		[ProducesResponseType(typeof(byte[]), 200)]
		public async Task<IActionResult> GetLegalIssuesAsync()
		{
			return Ok(await System.IO.File.ReadAllBytesAsync(LegalIssuesPath));
		}

		/// <summary>
		/// Gets the latest version of the PrivacyPolicy document.
		/// </summary>
		/// <returns>PrivacyPolicy text document bytes in BASE64 format.</returns>
		/// <response code="200">PrivacyPolicy</response>
		[HttpGet("privacypolicy")]
		[ProducesResponseType(typeof(byte[]), 200)]
		public async Task<IActionResult> GetPrivacyPolicyAsync()
		{
			return Ok(await System.IO.File.ReadAllBytesAsync(PrivacyPolicyPath));
		}

		/// <summary>
		/// Gets the latest version of the TermsAndConditions document.
		/// </summary>
		/// <returns>TermsAndConditions text document bytes in BASE64 format.</returns>
		/// <response code="200">TermsAndConditions</response>
		[HttpGet("termsandconditions")]
		[ProducesResponseType(typeof(byte[]), 200)]
		public async Task<IActionResult> GetTermsAndConditionsAsync()
		{
			return Ok(await System.IO.File.ReadAllBytesAsync(TermsAndConditionsPath));
		}
	}
}
