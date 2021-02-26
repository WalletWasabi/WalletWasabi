using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.JsonConverters.Bitcoin
{
	public class DefaultValueScriptAttribute : DefaultValueAttribute
	{
		public DefaultValueScriptAttribute(string json) : base(ScriptJsonConverter.Parse(json))
		{
		}
	}
}
