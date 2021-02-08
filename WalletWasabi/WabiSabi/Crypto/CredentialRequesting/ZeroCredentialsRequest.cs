using NBitcoin;
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
		public ZeroCredentialsRequest(Money deltaAmount, IEnumerable<CredentialPresentation> presented, IEnumerable<IssuanceRequest> requested, IEnumerable<Proof> proofs)
			: base(deltaAmount, presented, requested, proofs)
		{
			if (!IsNullRequest)
			{
				throw new InvalidOperationException($"{nameof(ZeroCredentialsRequest)} must be null request.");
			}
		}
	}
}
