using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record OutputRegistrationResponse(
		CredentialsResponse AmountCredentials,
		CredentialsResponse VsizeCredentials
	);
}
