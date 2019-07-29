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
			string host = HttpContext?.Request?.Host.Host;

			VirtualFileResult response;
			if (!string.IsNullOrWhiteSpace(host) && host.TrimEnd('/').EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
			{
				response = File("onion-index.html", "text/html");
			}
			else
			{
				response = File("index.html", "text/html");
			}

			response.LastModified = DateTimeOffset.UtcNow;
			return response;
		}
	}
}
