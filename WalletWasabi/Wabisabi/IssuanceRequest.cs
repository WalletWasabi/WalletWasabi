using System.Collections.Generic;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Wabisabi
{
	/// <summary>
	/// Represents a request for issuance a new credential.
	/// </summary>
	public class IssuanceRequest
	{
		internal IssuanceRequest(GroupElement ma, IEnumerable<GroupElement> bitCommitments)
		{
			Ma = ma;
			BitCommitments = bitCommitments;
		}

		/// <summary>
		/// Pedersen commitment to the credential amount.
		/// </summary>
		public GroupElement Ma { get; }
		
		/// <summary>
		/// Commitments the the value's bits decomposed amount.
		/// </summary>
		public IEnumerable<GroupElement> BitCommitments { get; }
	}
}