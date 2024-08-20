using NBitcoin;

namespace WalletWasabi.WabiSabi.Models;

public readonly struct CoordinationFeeRate
{
	public static readonly CoordinationFeeRate Zero = new();

	public decimal Rate => 0m;
	public Money PlebsDontPayThreshold => Money.Zero;

	public Money GetFee(Money amount)
	{
		return Money.Zero;
	}
}
