namespace WalletWasabi.WabiSabi.Models;

public record ConnectionConfirmationRequest(
	uint256 RoundId,
	Guid AliceId,
	ZeroCredentialsRequest ZeroAmountCredentialRequests,
	RealCredentialsRequest RealAmountCredentialRequests,
	ZeroCredentialsRequest ZeroVsizeCredentialRequests,
	RealCredentialsRequest RealVsizeCredentialRequests
);
