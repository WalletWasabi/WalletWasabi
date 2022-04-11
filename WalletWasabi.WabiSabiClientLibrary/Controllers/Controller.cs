using Microsoft.AspNetCore.Mvc;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.WabiSabiClientLibrary.Controllers.Helpers;
using WalletWasabi.WabiSabiClientLibrary.Crypto;
using WalletWasabi.WabiSabiClientLibrary.Filters;
using WalletWasabi.WabiSabiClientLibrary.Models;

namespace WalletWasabi.WabiSabiClientLibrary.Controllers;

[ApiController]
[ExceptionTranslateFilter]
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

	/// <summary>
	/// Given a set of effective input amounts registered by a participant and a set of effective input amounts
	/// registered by other participants, decompose the amounts registered by the participant into output amounts.
	/// </summary>
	[HttpPost("get-outputs-amounts")]
	public GetOutputAmountsResponse GetOutputAmounts(GetOutputAmountsRequest request)
	{
		return GetOutputAmountsHelper.GetOutputAmounts(request, _random);
	}

	[HttpPost("get-zero-credential-requests")]
	public GetZeroCredentialRequestsResponse GetZeroCredentialRequests(GetZeroCredentialRequestsRequest request)
	{
		return CredentialHelper.GetZeroCredentialRequests(request, _random);
	}

	[HttpPost("get-real-credential-requests")]
	public GetRealCredentialRequestsResponse GetRealCredentialRequests(GetRealCredentialRequestsRequest request)
	{
		return CredentialHelper.GetRealCredentialRequests(request, _random);
	}

	[HttpPost("get-credentials")]
	public GetCredentialsResponse GetCredentials(GetCredentialsRequest request)
	{
		return CredentialHelper.GetCredentials(request, _random);
	}

	public void Dispose()
	{
	}
}
