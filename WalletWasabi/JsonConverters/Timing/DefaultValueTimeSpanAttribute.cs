using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.JsonConverters.Timing
{
	public class DefaultValueTimeSpanAttribute : DefaultValueAttribute
	{
		public DefaultValueTimeSpanAttribute(string json) : base(TimeSpanJsonConverter.Parse(json))
		{
		}
	}
}
