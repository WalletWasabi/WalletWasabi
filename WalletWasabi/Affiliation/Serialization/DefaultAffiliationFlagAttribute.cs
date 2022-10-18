using System.ComponentModel;

namespace WalletWasabi.Affiliation.Serialization;

public class DefaultAffiliationFlagAttribute : DefaultValueAttribute
{
	public DefaultAffiliationFlagAttribute() : base(AffiliationFlag.Default)
	{
	}
}
