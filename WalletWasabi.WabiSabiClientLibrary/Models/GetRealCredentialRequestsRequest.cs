using WabiSabi.Crypto;
using WabiSabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabiClientLibrary.Models;

public record GetRealCredentialRequestsRequest(
	CredentialIssuerParameters CredentialIssuerParameters,
	long MaxCredentialValue,
	long[] AmountsToRequest,
	Credential[] CredentialsToPresent
);
