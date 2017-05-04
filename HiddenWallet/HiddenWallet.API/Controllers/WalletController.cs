using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HiddenWallet.API.Models;
using HiddenWallet.API.Wrappers;

namespace HiddenWallet.API.Controllers
{
	[Route("api/v1/[controller]")]
	public class WalletController : Controller
	{
		[HttpGet]
		public string Test()
		{
			return "test";
		}

		[Route("create")]
		[HttpPost]
		public IActionResult Create([FromBody]PasswordModel request)
		{
			if (request == null || request.Password == null)
			{
				return new ObjectResult(new FailureResponse { Message = "Bad request", Details = "" });
			}

			try
			{
				WalletCreateResponse response = Global.WalletWrapper.Create(request.Password);

				return new ObjectResult(response);
			}
			catch(Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("recover")]
		[HttpPost]
		public IActionResult Recover([FromBody]WalletRecoverRequest request)
		{
			if (request == null || request.Password == null || request.Mnemonic == null)
			{
				return new ObjectResult(new FailureResponse { Message = "Bad request", Details = "" });
			}

			try
			{
				Global.WalletWrapper.Recover(request.Password, request.Mnemonic, request.CreationTime);

				return new ObjectResult(new SuccessResponse());
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("load")]
		[HttpPost]
        public IActionResult Load([FromBody]PasswordModel request)
        {
			if (request == null || request.Password == null)
			{
				return new ObjectResult(new FailureResponse { Message = "Bad request", Details = "" });
			}

			try
			{
				Global.WalletWrapper.Load(request.Password);

				return new ObjectResult(new SuccessResponse());
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("wallet-exists")]
		[HttpGet]
		public IActionResult WalletExists()
		{
			try
			{
				if (Global.WalletWrapper.WalletExists)
				{
					return new ObjectResult(new YesNoResponse { Value = true });
				}
				return new ObjectResult(new YesNoResponse { Value = false });
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("status")]
		[HttpGet]
		public IActionResult Status()
		{
			try
			{
				return new ObjectResult(Global.WalletWrapper.GetStatusResponse());
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("shutdown")]
		[HttpGet]
		public IActionResult Shutdown()
		{
			try
			{
				try
				{
					Global.WalletWrapper.EndAsync().Wait();

					return new ObjectResult(new SuccessResponse());
				}
				finally
				{
					// wait until the call returns
					Task.Delay(1000).ContinueWith(_ => Environment.Exit(0));
				}
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("balances")]
		[HttpGet]
		public IActionResult Balances([FromBody]AccountModel request)
		{
			try
			{
				if (!Global.WalletWrapper.IsDecrypted)
				{
					return new ObjectResult(new FailureResponse { Message = "Wallet isn't decrypted" });
				}
				var trimmed = request.Account;
				if (String.Equals(trimmed, "alice", StringComparison.OrdinalIgnoreCase))
				{
					return new ObjectResult(new BalancesResponse { Available = 32.1m, Incoming = 0.23534219m });
				}
				else if (String.Equals(trimmed, "bob", StringComparison.OrdinalIgnoreCase))
				{
					return new ObjectResult(new BalancesResponse { Available = 0.34342m, Incoming = 5m });
				}
				else return new ObjectResult(new FailureResponse { Message = "Wrong account" });				
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}
	}
}
