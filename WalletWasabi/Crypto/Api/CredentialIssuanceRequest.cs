using System.Collections.Generic;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.Api
{
	public class CredentialIssuanceRequest
	{
		public CredentialIssuanceRequest(GroupElement ma, IEnumerable<GroupElement> bitCommitments)
		{
			Ma = ma;
			BitCommitments = bitCommitments;
		}

		public GroupElement Ma { get; }
		public IEnumerable<GroupElement> BitCommitments { get; }
	}
}