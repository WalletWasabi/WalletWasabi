using System.Collections.Generic;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Wabisabi
{
	/// <summary>
	/// Represents a request for issuing a new credential.
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
		/// Pedersen commitments to the credential amount's binary decomposition.
		/// </summary>
		public IEnumerable<GroupElement> BitCommitments { get; }
	}
}