using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record OutputRegistrationResponse(
		string UnsignedTransactionSecret,
		CredentialsResponse AmountCredentials,
		CredentialsResponse WeightCredentials
	);
}
