using System.Collections.Immutable;
using System.ComponentModel;

namespace WalletWasabi.Affiliation.Serialization;

public class DefaultAffiliateServersAttribute : DefaultValueAttribute
{
	public DefaultAffiliateServersAttribute() : base(ImmutableDictionary<string, string>.Empty)
	{
	}
}
