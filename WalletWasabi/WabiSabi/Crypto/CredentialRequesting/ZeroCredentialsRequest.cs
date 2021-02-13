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
