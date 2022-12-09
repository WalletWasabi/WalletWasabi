using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Affiliation.Models;

public record AffiliateInformation
(
	IEnumerable<AffiliationFlag> RunningAffiliateServers,
	IReadOnlyDictionary<uint256, IReadOnlyDictionary<AffiliationFlag, byte[]>> PaymentData
)
{
	public static AffiliateInformation Empty = new AffiliateInformation(Enumerable.Empty<AffiliationFlag>(), ImmutableDictionary<uint256, IReadOnlyDictionary<AffiliationFlag, byte[]>>.Empty);
}
