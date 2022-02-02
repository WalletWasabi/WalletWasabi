using NBitcoin;
using System.ComponentModel;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.JsonConverters;

public class DefaultValueCoordinationFeeRateAttribute : DefaultValueAttribute
{
	public DefaultValueCoordinationFeeRateAttribute(double feeRate, double plebsDontPayThreshold)
		: base(new CoordinationFeeRate((decimal)feeRate, Money.Coins((decimal)plebsDontPayThreshold)))
	{
	}
}
