using HiddenWallet.FullSpvWallet.ChaumianCoinJoin;
using HiddenWallet.SharedApi.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon.Controllers
{
	[Route("api/v1/[controller]")]
	public class TumblerController : Controller
    {
		[HttpGet]
		public string Test()
		{
			return "test";
		}

		[Route("connection")]
		[HttpGet]
		public async Task<IActionResult> ConnectionAsync()
		{ 
			try
			{
				CoinJoinService coinJoinService = Global.WalletWrapper.WalletJob.CoinJoinService;
				if (coinJoinService.TumblerConnection == null)
				{
					await coinJoinService.SubscribePhaseChangeAsync();
				}
				if (coinJoinService.TumblerConnection == null)
				{
					return new ObjectResult(new FailureResponse { Message = "", Details = "" });
				}
				else
				{
					if(coinJoinService.StatusResponse == null)
					{
						coinJoinService.StatusResponse = await coinJoinService.TumblerClient.GetStatusAsync(CancellationToken.None);
					}

					return new ObjectResult(new SuccessResponse());
				}
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}
	}
}
