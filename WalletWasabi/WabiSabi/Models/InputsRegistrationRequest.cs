using System;
using System.Collections.Generic;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record InputsRegistrationRequest(
		Guid RoundId,
		IEnumerable<InputRoundSignaturePair> InputRoundSignaturePairs,
		ZeroCredentialsRequest ZeroAmountCredentialRequests,
		ZeroCredentialsRequest ZeroWeightCredentialRequests
	);
}
