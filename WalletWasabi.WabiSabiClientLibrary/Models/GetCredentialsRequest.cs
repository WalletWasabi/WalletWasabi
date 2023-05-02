using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;

namespace WalletWasabi.WabiSabiClientLibrary.Models;

public record GetCredentialsRequest(
	long MaxAmountCredentialValue,
	CredentialIssuerParameters CredentialIssuerParameters,
	CredentialsResponse CredentialsResponse,
	CredentialsResponseValidation CredentialsValidationData
);
