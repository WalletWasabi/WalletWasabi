using System;
using NBitcoin;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record OutputRegistrationRequest(
		uint256 RoundId,
		Script Script,
		RealCredentialsRequest AmountCredentialRequests,
		RealCredentialsRequest VsizeCredentialRequests
	);
}
