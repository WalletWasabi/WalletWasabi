using Microsoft.AspNetCore.Mvc;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.WabiSabiClientLibrary.Crypto;

namespace WalletWasabi.WabiSabiClientLibrary.Controllers;

[ApiController]
[Produces("application/json")]
public class Controller : ControllerBase, IDisposable
{
	private readonly WasabiRandom _random;
	private readonly Global _global;

	public Controller(Global global, WasabiRandom random)
	{
		_random = random;
		_global = global;
	}

	public void Dispose()
	{
	}
}
