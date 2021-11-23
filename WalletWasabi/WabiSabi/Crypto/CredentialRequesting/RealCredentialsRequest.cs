using System;
using System.Collections.Generic;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Crypto.CredentialRequesting
{
	public record RealCredentialsRequest : CredentialsRequest
	{
		public RealCredentialsRequest(
			long delta,
			IEnumerable<CredentialPresentation> presented,
			IEnumerable<IssuanceRequest> requested,
			IEnumerable<Proof> proofs)
			: base(delta, presented, requested, proofs)
		{
			if (IsNullRequest)
			{
				throw new InvalidOperationException($"{nameof(RealCredentialsRequest)} must not be null request.");
			}
		}
	}
}
