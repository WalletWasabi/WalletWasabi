using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.JsonConverters.Bitcoin
{
	public class DefaultValueMoneyBtcAttribute : DefaultValueAttribute
	{
		public DefaultValueMoneyBtcAttribute(string json) : base(MoneyBtcJsonConverter.Parse(json))
		{
		}
	}
}
