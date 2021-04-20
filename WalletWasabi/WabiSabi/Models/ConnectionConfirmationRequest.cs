using System;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record ConnectionConfirmationRequest(
		Guid RoundId,
		Guid AliceId, 
		ZeroCredentialsRequest ZeroAmountCredentialRequests, 
		RealCredentialsRequest RealAmountCredentialRequests, 
		ZeroCredentialsRequest ZeroVsizeCredentialRequests, 
		RealCredentialsRequest RealVsizeCredentialRequests
	);
}
