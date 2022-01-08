using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models;

public record ConnectionConfirmationResponse(
	CredentialsResponse ZeroAmountCredentials,
	CredentialsResponse ZeroVsizeCredentials,
	CredentialsResponse? RealAmountCredentials = null,
	CredentialsResponse? RealVsizeCredentials = null
);
