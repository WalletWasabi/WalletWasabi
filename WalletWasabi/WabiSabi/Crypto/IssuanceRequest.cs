using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.WabiSabi.Crypto;

/// <summary>
/// Represents a request for issuing a new credential.
/// </summary>
public record IssuanceRequest : IEquatable<IssuanceRequest>
{
	[JsonConstructor]
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

	public override int GetHashCode()
	{
		int hc = 0;

		foreach (var element in BitCommitments)
		{
			hc ^= element.GetHashCode();
			hc = (hc << 7) | (hc >> (32 - 7));
		}

		return HashCode.Combine(Ma.GetHashCode(), hc);
	}

	public virtual bool Equals(IssuanceRequest? other)
	{
		if (other is null)
		{
			return false;
		}

		bool isEqual = Ma == other.Ma
			&& BitCommitments.SequenceEqual(other.BitCommitments);

		return isEqual;
	}
}
