using Microsoft.AspNetCore.Mvc;

namespace WalletWasabi.Backend.Controllers;

[Route("")]
public class HomeController : ControllerBase
{
	[HttpGet("")]
	public IActionResult Index()
	{
		string host = HttpContext.Request.Host.Host;

		VirtualFileResult response = !string.IsNullOrWhiteSpace(host) && host.TrimEnd('/').EndsWith(".onion", StringComparison.OrdinalIgnoreCase)
			? File("onion-index.html", "text/html")
			: File("index.html", "text/html");

		response.LastModified = DateTimeOffset.UtcNow;
		return response;
	}
}
