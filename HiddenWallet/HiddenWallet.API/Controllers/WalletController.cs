using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HiddenWallet.API.Models;
using HiddenWallet.API.Wrappers;
using HBitcoin.KeyManagement;

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

		[Route("balances/{account}")]
		[HttpGet]
		public IActionResult Balances(string account)
		{
			try
			{
				var fail = GetAccount(account, out SafeAccount safeAccount);
				if (fail != null) return new ObjectResult(fail);
				
				return new ObjectResult(new BalancesResponse
				{
					Available = Global.WalletWrapper.GetAvailable(safeAccount).ToString(false, true),
					Incoming = Global.WalletWrapper.GetIncoming(safeAccount).ToString(false, true)
				});
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		/// <returns>null if didn't fail</returns>
		private FailureResponse GetAccount(string account, out SafeAccount safeAccount)
		{
			safeAccount = null;
			if (account == null)
				return new FailureResponse { Message = "No request body specified" };

			if (!Global.WalletWrapper.IsDecrypted)
				return new FailureResponse { Message = "Wallet isn't decrypted" };

			var trimmed = account;
			if (String.Equals(trimmed, "alice", StringComparison.OrdinalIgnoreCase))
			{
				safeAccount = Global.WalletWrapper.AliceAccount;
				return null;
			}
			else if (String.Equals(trimmed, "bob", StringComparison.OrdinalIgnoreCase))
			{
				safeAccount = Global.WalletWrapper.BobAccount;
				return null;
			}
			else return new FailureResponse { Message = "Wrong account" };
		}

		[Route("receive/{account}")]
		[HttpGet]
		public IActionResult Receive(string account)
		{
			try
			{
				var fail = GetAccount(account, out SafeAccount safeAccount);
				if (fail != null) return new ObjectResult(fail);

				return new ObjectResult(Global.WalletWrapper.GetReceiveResponse(safeAccount));
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("history/{account}")]
		[HttpGet]
		public IActionResult History(string account)
		{
			try
			{
				var fail = GetAccount(account, out SafeAccount safeAccount);
				if (fail != null) return new ObjectResult(fail);

				return new ObjectResult(Global.WalletWrapper.GetHistoryResponse(safeAccount));
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}
	}
}
