using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Randomness;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabiClientLibrary.Models;

namespace WalletWasabi.WabiSabiClientLibrary.Controllers.Helpers;

public class CredentialHelper
{
	public static GetRealCredentialRequestsResponse GetRealCredentialRequests(GetRealCredentialRequestsRequest request, WasabiRandom secureRandom)
	{
		WabiSabiClient wabiSabiClient = new(request.CredentialIssuerParameters, secureRandom, request.MaxCredentialValue);
		RealCredentialsRequestData requestData = wabiSabiClient.CreateRequest(request.AmountsToRequest, request.CredentialsToPresent, CancellationToken.None);
		return new GetRealCredentialRequestsResponse(requestData);
	}

	public static GetZeroCredentialRequestsResponse GetZeroCredentialRequests(GetZeroCredentialRequestsRequest request, WasabiRandom secureRandom)
	{
		WabiSabiClient wabiSabiClient = new(request.CredentialIssuerParameters, secureRandom, request.MaxAmountCredentialValue);
		ZeroCredentialsRequestData requestData = wabiSabiClient.CreateRequestForZeroAmount();
		return new GetZeroCredentialRequestsResponse(requestData);
	}

	public static GetCredentialsResponse GetCredentials(GetCredentialsRequest request, WasabiRandom secureRandom)
	{
		WabiSabiClient wabiSabiClient = new(request.CredentialIssuerParameters, secureRandom, request.MaxAmountCredentialValue);
		IEnumerable<Credential> credentials = wabiSabiClient.HandleResponse(request.CredentialsResponse, request.CredentialsValidationData);
		return new GetCredentialsResponse(credentials.ToArray());
	}
}
