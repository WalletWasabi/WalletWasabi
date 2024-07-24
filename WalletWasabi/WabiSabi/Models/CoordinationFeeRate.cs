using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models;

public readonly struct CoordinationFeeRate
{
	public static readonly CoordinationFeeRate Zero = new(0);

	public CoordinationFeeRate(decimal rate)
	{
		Rate = Guard.InRangeAndNotNull(nameof(rate), rate, 0m, Constants.AbsoluteMaxCoordinationFeeRate);
	}

	public decimal Rate { get; }

	public Money GetFee(Money amount)
	{
		return Money.Satoshis(Math.Floor(amount.Satoshi * Rate));
	}
}
