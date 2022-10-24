using System.ComponentModel;

namespace WalletWasabi.JsonConverters;

public class DefaultValueIntegerArrayAttribute : DefaultValueAttribute
{
	public DefaultValueIntegerArrayAttribute(string json) : base(IntegerArrayJsonConverter.Parse(json))
	{
	}
}
