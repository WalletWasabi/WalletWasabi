using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models
{
	public struct CoordinationFeeRate
	{
		public static readonly CoordinationFeeRate Zero = new(0);

		public CoordinationFeeRate(decimal rate)
		{
			Rate = Guard.InRangeAndNotNull(nameof(rate), rate, 0m, 0.01m);
		}

		public decimal Rate { get; }

		public Money GetFee(Money amount)
		{
			// Under 100 000 satoshis plebs don't have to pay.
			if (amount <= Money.Coins(0.001m))
			{
				return Money.Zero;
			}
			else
			{
				return Money.Satoshis(Math.Floor(amount.Satoshi * Rate));
			}
		}
	}
}
