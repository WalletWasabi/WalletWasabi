using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
