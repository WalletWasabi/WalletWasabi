namespace WalletWasabi.WabiSabi.Models;

public record ReissueCredentialRequest(
	uint256 RoundId,
	RealCredentialsRequest RealAmountCredentialRequests,
	RealCredentialsRequest RealVsizeCredentialRequests,
	ZeroCredentialsRequest ZeroAmountCredentialRequests,
	ZeroCredentialsRequest ZeroVsizeCredentialsRequests
);
