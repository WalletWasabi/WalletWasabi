using System.ComponentModel;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.JsonConverters
{
	public class DefaultValueCoordinationFeeRateAttribute : DefaultValueAttribute
	{
		public DefaultValueCoordinationFeeRateAttribute(double value) : base(new CoordinationFeeRate((decimal)value))
		{
		}
	}
}
