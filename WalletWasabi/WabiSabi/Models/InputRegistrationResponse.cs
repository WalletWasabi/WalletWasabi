using System;
using NBitcoin;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record InputRegistrationResponse(
		uint256 AliceId,
		CredentialsResponse AmountCredentials,
		CredentialsResponse VsizeCredentials
	);
}
