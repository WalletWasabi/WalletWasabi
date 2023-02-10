using System.Collections.Immutable;

namespace WalletWasabi.Affiliation.Models;

public record AffiliateInformation
(
	ImmutableArray<AffiliationFlag> RunningAffiliateServers,
	ImmutableDictionary<string, ImmutableDictionary<AffiliationFlag, byte[]>> CoinjoinRequests)
{
	public static readonly AffiliateInformation Empty = new(
		RunningAffiliateServers: ImmutableArray<AffiliationFlag>.Empty,
		CoinjoinRequests: ImmutableDictionary<string, ImmutableDictionary<AffiliationFlag, byte[]>>.Empty);
}
