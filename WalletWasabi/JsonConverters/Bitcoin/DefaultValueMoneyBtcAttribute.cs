using System.ComponentModel;

namespace WalletWasabi.JsonConverters.Bitcoin;

public class DefaultValueMoneyBtcAttribute : DefaultValueAttribute
{
	public DefaultValueMoneyBtcAttribute(string json) : base(MoneyBtcJsonConverter.Parse(json))
	{
	}
}
