using System.Collections.Immutable;

namespace WalletWasabi.Affiliation.Models;

public record AffiliateInformation
(
	ImmutableArray<string> RunningAffiliateServers,
	ImmutableDictionary<string, ImmutableDictionary<string, byte[]>> AffiliateData)
{
	public static readonly AffiliateInformation Empty = new(
		RunningAffiliateServers: ImmutableArray<string>.Empty,
		AffiliateData: ImmutableDictionary<string, ImmutableDictionary<string, byte[]>>.Empty);
}
