using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Affiliation.Models;

public record AffiliateInformation
(
	ImmutableArray<AffiliationFlag> RunningAffiliateServers,
	ImmutableDictionary<string, ImmutableDictionary<AffiliationFlag, byte[]>> CoinjoinRequests
)
{
	public static readonly AffiliateInformation Empty = new(ImmutableArray<AffiliationFlag>.Empty, ImmutableDictionary<string, ImmutableDictionary<AffiliationFlag, byte[]>>.Empty);
}
