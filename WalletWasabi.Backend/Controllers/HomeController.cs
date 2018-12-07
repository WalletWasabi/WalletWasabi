using Microsoft.AspNetCore.Mvc;
using System;

namespace WalletWasabi.Backend.Controllers
{
	[Route("")]
	public class HomeController : Controller
	{
		[HttpGet("")]
		public ActionResult Index()
		{
			VirtualFileResult response = File("index.html", "text/html");
			response.LastModified = DateTimeOffset.UtcNow;
			return response;
		}
	}
}
