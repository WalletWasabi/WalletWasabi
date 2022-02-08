using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge;

public record Proof
{
	[JsonConstructor]
	internal Proof(GroupElementVector publicNonces, ScalarVector responses)
	{
		Guard.NotNullOrInfinity(nameof(publicNonces), publicNonces);
		Guard.NotNullOrEmpty(nameof(responses), responses);

		PublicNonces = publicNonces;
		Responses = responses;
	}

	public GroupElementVector PublicNonces { get; }
	public ScalarVector Responses { get; }

	public override int GetHashCode()
	{
		return HashCode.Combine(PublicNonces, Responses);
	}

	public virtual bool Equals(Proof? other)
	{
		if (other is null)
		{
			return false;
		}

		bool isEqual = PublicNonces == other.PublicNonces
			&& Responses == other.Responses;

		return isEqual;
	}
}
