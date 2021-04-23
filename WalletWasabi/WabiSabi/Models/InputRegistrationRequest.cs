using System;
using NBitcoin;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record InputRegistrationRequest(
		Guid RoundId,
		OutPoint Input,
		byte[] RoundSignature,
		ZeroCredentialsRequest ZeroAmountCredentialRequests,
		ZeroCredentialsRequest ZeroVsizeCredentialRequests
	);
}
