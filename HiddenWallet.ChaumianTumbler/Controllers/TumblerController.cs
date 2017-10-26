using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HiddenWallet.SharedApi.Models;
using HiddenWallet.ChaumianTumbler.Models;

namespace HiddenWallet.ChaumianTumbler.Controllers
{
	[Route("api/v1/[controller]")]
	public class TumblerController : Controller
    {
		//	Used to simulate a client connecting. Returns ClientTest.html which contains JavaScript
		//	to connect to the SignalR Hub - ChaumianTumblerHub. In real use the clients will connect 
		//	directly to the hub via C# code in HiddenWallet.
		[Route("client-test")]
		[HttpGet]
		public IActionResult ClientTest()
		{
			return View();
		}

		//	Used to simulate submissions via MVC of data such as InputRequest which get actioned by 
		//	ChaumianTumbler which then broadcasts messages back to the client connected using 
		//	'client-test' above. 
		[Route("client-test-submit")]
		[HttpGet]
		public void TestInputsSubmit(InputsRequest request)
		{
			//	This will simply mock up the submission of data to the MVC controllers.
			//	Once the collection inside ChaumianTumbler reaches 3 (just used for example
			//	purposes until actual Chaumian code implemented) it will trigger a call to the 
			//	hub to broadcast a new state to the clients.

			Random rnd = new Random();
			int random = rnd.Next(1, 1000);

			InputsRequest testRequest = new InputsRequest { BlindedOutput = random.ToString(), ChangeOutput = "CHANGE TEST" };

			ChaumianTumbler ct = ChaumianTumbler.Instance;
			
			ct.ProcessInputsRequest(testRequest); //If collection.Count > 3 a SignalR broadcast is made to clients that connected via client-test
		}

		[Route("status")]
		[HttpGet]
		public IActionResult Status()
		{
			try
			{
				throw new NotImplementedException();
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("inputs")]
		[HttpPost]
		public IActionResult Inputs(InputsRequest request)
		{
			try
			{
				throw new NotImplementedException();
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("input-registration-status")]
		[HttpGet]
		public IActionResult InputRegistrationStatus()
		{
			try
			{
				throw new NotImplementedException();
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("connection-confirmation")]
		[HttpPost]
		public IActionResult ConnectionConfirmation(ConnectionConfirmationRequest request)
		{
			try
			{
				throw new NotImplementedException();
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("output")]
		[HttpPost]
		public IActionResult Output(OutputRequest request)
		{
			try
			{
				throw new NotImplementedException();
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("coinjoin")]
		[HttpGet]
		public IActionResult CoinJoin()
		{
			try
			{
				throw new NotImplementedException();
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("signature")]
		[HttpPost]
		public IActionResult Signature(SignatureRequest request)
		{
			try
			{
				throw new NotImplementedException();
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}
	}
}
