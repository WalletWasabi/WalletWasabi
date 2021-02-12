using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.JsonConverters.Collections
{
	public class DefaultValueStringSetAttribute : DefaultValueAttribute
	{
		public DefaultValueStringSetAttribute(string json)
			: base(JsonConvert.DeserializeObject<ISet<string>>(json))
		{
		}
	}
}
