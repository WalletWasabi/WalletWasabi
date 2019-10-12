using Microsoft.AspNetCore.Mvc;
using NBitcoin.RPC;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To acquire administrative data about the software.
	/// </summary>
	[Produces("application/json")]
	[Route("api/[controller]")]
	public class SoftwareController : Controller
	{
		private readonly VersionsResponse VersionsResponse = new VersionsResponse { ClientVersion = Constants.ClientVersion.ToString(3), BackendMajorVersion = Constants.BackendMajorVersion };

		public Global Global { get; }

		private RPCClient RpcClient => Global.RpcClient;

		public SoftwareController(Global global)
		{
			Global = global;
		}

		/// <summary>
		/// Gets the latest versions of the client and backend.
		/// </summary>
		/// <returns>ClientVersion, BackendMajorVersion.</returns>
		/// <response code="200">ClientVersion, BackendMajorVersion.</response>
		[HttpGet("versions")]
		[ProducesResponseType(typeof(VersionsResponse), 200)]
		public VersionsResponse GetVersions()
		{
			return VersionsResponse;
		}

		[HttpGet("status")]
		[ProducesResponseType(typeof(StatusResponse), 200)]
		[ResponseCache(Duration = 10, Location = ResponseCacheLocation.Client)]
		public async Task<StatusResponse> GetStatusAsync()
		{
			var result = new StatusResponse();

			try
			{
				var lastFilter = Global.IndexBuilderService.GetLastFilter();
				var lastFilterHash = lastFilter.BlockHash;

				var bestHash = await RpcClient.GetBestBlockHashAsync();
				var lastBlock = await RpcClient.GetBlockAsync(bestHash);
				var prevHash = lastBlock.Header.HashPrevBlock;

				if (bestHash == lastFilterHash || prevHash == lastFilterHash)
				{
					result.FilterCreationActive = true;
				}
			}
			catch
			{
			}

			return result;
		}
	}
}
