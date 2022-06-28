using Microsoft.AspNetCore.Mvc;

namespace WalletWasabi.Backend.Controllers;

[Route("")]
public class HomeController : ControllerBase
{
	[HttpGet("")]
	public IActionResult Index()
	{
		VirtualFileResult response = File("index.html", "text/html");
		response.LastModified = DateTimeOffset.UtcNow;
		return response;
	}
}
