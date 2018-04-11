using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To interact with the Chaumian CoinJoin Coordinator.
	/// </summary>
	[Produces("application/json")]
	[Route("api/v1/btc/[controller]")]
	public class ChaumianCoinJoinController : Controller
    {

    }
}