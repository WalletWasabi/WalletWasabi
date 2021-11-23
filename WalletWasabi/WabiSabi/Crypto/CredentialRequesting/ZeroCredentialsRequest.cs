using System;
using System.Collections.Generic;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Crypto.CredentialRequesting
{
	public record ZeroCredentialsRequest : CredentialsRequest
	{
		public ZeroCredentialsRequest(
			IEnumerable<IssuanceRequest> requested,
			IEnumerable<Proof> proofs)
			: base(0, Array.Empty<CredentialPresentation>(), requested, proofs)
		{
			if (!IsNullRequest)
			{
				throw new InvalidOperationException($"{nameof(ZeroCredentialsRequest)} must be null request.");
			}
		}
	}
}
