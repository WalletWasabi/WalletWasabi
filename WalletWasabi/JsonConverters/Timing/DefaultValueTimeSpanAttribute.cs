using System.ComponentModel;

namespace WalletWasabi.JsonConverters.Timing;

public class DefaultValueTimeSpanAttribute : DefaultValueAttribute
{
	public DefaultValueTimeSpanAttribute(string json) : base(TimeSpanJsonConverter.Parse(json))
	{
	}
}
