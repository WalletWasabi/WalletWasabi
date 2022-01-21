using System.ComponentModel;

namespace WalletWasabi.JsonConverters.Bitcoin;

public class DefaultValueScriptAttribute : DefaultValueAttribute
{
	public DefaultValueScriptAttribute(string json) : base(ScriptJsonConverter.Parse(json))
	{
	}
}
