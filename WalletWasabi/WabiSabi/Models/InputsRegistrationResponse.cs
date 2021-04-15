using System;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record InputsRegistrationResponse(
		Guid AliceId,
		CredentialsResponse AmountCredentials,
		CredentialsResponse VsizeCredentials
	);
}
