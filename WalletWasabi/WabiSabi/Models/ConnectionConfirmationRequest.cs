using System;
using NBitcoin;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record ConnectionConfirmationRequest(
		uint256 RoundId,
		uint256 AliceId, 
		ZeroCredentialsRequest ZeroAmountCredentialRequests, 
		RealCredentialsRequest RealAmountCredentialRequests, 
		ZeroCredentialsRequest ZeroVsizeCredentialRequests, 
		RealCredentialsRequest RealVsizeCredentialRequests
	);
}
