using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

namespace WalletWasabi.JsonConverters.Collections;

public class DefaultValueEmptyIntegerList : DefaultValueAttribute
{
	public DefaultValueEmptyIntegerList()
		: base(JsonConvert.DeserializeObject<IEnumerable<int>>(""))
	{
	}
}
