using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;
using WalletWasabi.Services;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To acquire administrative data about the software.
	/// </summary>
	[Produces("application/json")]
	[Route("api/[controller]")]
	public class SoftwareController : Controller
	{
		private readonly VersionsResponse VersionsResponse;

		public SoftwareController()
		{
			VersionsResponse = new VersionsResponse()
			{
				ClientVersion = Constants.ClientVersion.ToString(3),
				BackendMajorVersion = Constants.BackendMajorVersion,
				LegalDocsVersion = Constants.LegalDocsVersion.ToString(4)
			};
		}

		/// <summary>
		/// Gets the latest versions of the client, backend and legal docs.
		/// </summary>
		/// <returns>ClientVersion, BackendMajorVersion, LegalDocsVersion.</returns>
		/// <response code="200">ClientVersion, BackendMajorVersion, LegalDocsVersion.</response>
		[HttpGet("versions")]
		[ProducesResponseType(typeof(VersionsResponse), 200)]
		public VersionsResponse GetVersions()
		{
			return VersionsResponse;
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
			using var memoryStream = new MemoryStream();
			await EmbeddedResourceHelper.GetResourceAsync(LegalDocsManager.EmbeddedResourceLegalIssues, memoryStream);
			return Ok(memoryStream.ToArray());
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
			using var memoryStream = new MemoryStream();
			await EmbeddedResourceHelper.GetResourceAsync(LegalDocsManager.EmbeddedResourcePrivacyPolicy, memoryStream);
			return Ok(memoryStream.ToArray());
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
			using var memoryStream = new MemoryStream();
			await EmbeddedResourceHelper.GetResourceAsync(LegalDocsManager.EmbeddedResourceTermsAndConditions, memoryStream);
			return Ok(memoryStream.ToArray());
		}
	}
}
