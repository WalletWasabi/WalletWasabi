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
	public class ZeroCredentialsRequest : CredentialsRequest
	{
		public ZeroCredentialsRequest(
			long delta,
			IEnumerable<CredentialPresentation> presented,
			IEnumerable<IssuanceRequest> requested,
			IEnumerable<Proof> proofs)
			: base(delta, presented, requested, proofs)
		{
			if (!IsNullRequest)
			{
				throw new InvalidOperationException($"{nameof(ZeroCredentialsRequest)} must be null request.");
			}
		}
	}
}
