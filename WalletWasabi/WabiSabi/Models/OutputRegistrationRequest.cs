using System;
using NBitcoin;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record OutputRegistrationRequest(
		Guid RoundId,
		Script Script,
		RealCredentialsRequest AmountCredentialRequests,
		RealCredentialsRequest WeightCredentialRequests
	);
}
