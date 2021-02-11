using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.JsonConverters.Collections
{
	public class DefaultValueStringCollectionAttribute : DefaultValueAttribute
	{
		public DefaultValueStringCollectionAttribute(string json)
			: base(JsonConvert.DeserializeObject<IEnumerable<string>>(json))
		{
		}
	}
}
