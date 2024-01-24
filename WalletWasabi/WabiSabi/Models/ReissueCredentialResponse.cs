using WabiSabi.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models;

public record ReissueCredentialResponse(
	CredentialsResponse RealAmountCredentials,
	CredentialsResponse RealVsizeCredentials,
	CredentialsResponse ZeroAmountCredentials,
	CredentialsResponse ZeroVsizeCredentials
);
