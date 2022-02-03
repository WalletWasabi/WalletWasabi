using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models;

public record InputRegistrationResponse(
	Guid AliceId,
	CredentialsResponse AmountCredentials,
	CredentialsResponse VsizeCredentials,
	bool IsPayingZeroCoordinationFee
);
