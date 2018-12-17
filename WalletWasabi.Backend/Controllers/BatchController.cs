using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To make batched requests.
	/// </summary>
	[Produces("application/json")]
	[Route("api/v" + Helpers.Constants.BackendMajorVersion + "/btc/[controller]")]
	public class BatchController : Controller
	{
		public BlockchainController BlockchainController { get; }
		public ChaumianCoinJoinController ChaumianCoinJoinController { get; }
		public HomeController HomeController { get; }
		public OffchainController OffchainController { get; }

		public BatchController(BlockchainController blockchainController, ChaumianCoinJoinController chaumianCoinJoinController, HomeController homeController, OffchainController offchainController)
		{
			BlockchainController = blockchainController;
			ChaumianCoinJoinController = chaumianCoinJoinController;
			HomeController = homeController;
			OffchainController = offchainController;
		}

		[HttpPost("synchronize")]
		public IActionResult PostSynchronizeAsync()
		{
			return Ok();
		}
	}
}
