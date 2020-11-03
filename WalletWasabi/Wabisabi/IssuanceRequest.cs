using System.Collections.Generic;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Wabisabi
{
	public class IssuanceRequest
	{
		internal IssuanceRequest(GroupElement ma, IEnumerable<GroupElement> bitCommitments)
		{
			Ma = ma;
			BitCommitments = bitCommitments;
		}

		public GroupElement Ma { get; }

		public IEnumerable<GroupElement> BitCommitments { get; }
	}
}