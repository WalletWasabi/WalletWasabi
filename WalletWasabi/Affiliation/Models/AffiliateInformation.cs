using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Affiliation.Models;

public record AffiliateInformation
(
	ImmutableArray<AffiliationFlag> RunningAffiliateServers,
	ImmutableDictionary<uint256, ImmutableDictionary<AffiliationFlag, byte[]>> CoinjoinRequests
)
{
	public static readonly AffiliateInformation Empty = new(ImmutableArray<AffiliationFlag>.Empty, ImmutableDictionary<uint256, ImmutableDictionary<AffiliationFlag, byte[]>>.Empty);
}
