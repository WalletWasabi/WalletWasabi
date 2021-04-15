using System;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record ReissueCredentialRequest(
		Guid RoundId,
		RealCredentialsRequest RealAmountCredentialRequests,
		ZeroCredentialsRequest ZeroAmountCredentialRequests
	);
}
