using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters.Collections;

public class DefaultValueStringSetAttribute : DefaultValueAttribute
{
	public DefaultValueStringSetAttribute(string json)
		: base(JsonConvert.DeserializeObject<ISet<string>>(json))
	{
	}
}
