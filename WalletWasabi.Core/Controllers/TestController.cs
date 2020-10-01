using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WalletWasabi.Helpers;

namespace WalletWasabi.Core.Controllers
{
	/// <summary>
	/// Testing requests.
	/// </summary>
	[ApiController]
	[Produces("application/json")]
	[Route("api/v" + Constants.WasabiCoreMajorVersion + "/[controller]")]
	public class TestController : ControllerBase
	{
		/// <summary>
		/// Say hello to the world.
		/// </summary>
		[HttpGet("hello")]
		[ProducesResponseType(200)]
		[ProducesResponseType(400)]
		public IActionResult GetHello()
		{
			if (!ModelState.IsValid)
			{
				return BadRequest();
			}

			return Ok("World");
		}
	}
}
