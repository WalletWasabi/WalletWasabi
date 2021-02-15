using NBitcoin;
using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Crypto.CredentialRequesting
{
	public class RealCredentialsRequest : CredentialsRequest
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
