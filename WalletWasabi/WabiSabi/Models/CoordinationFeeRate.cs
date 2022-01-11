using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models
{
	public struct CoordinationFeeRate
	{
		public static readonly CoordinationFeeRate Zero = new(0, 0);

		public CoordinationFeeRate(decimal rate, Money plebsDontPayThreshold)
		{
			Rate = Guard.InRangeAndNotNull(nameof(rate), rate, 0m, 0.01m);
			PlebsDontPayThreshold = plebsDontPayThreshold;
		}

		public decimal Rate { get; }
		public Money PlebsDontPayThreshold { get; }

		public Money GetFee(Money amount)
		{
			// Under 100 000 satoshis plebs don't have to pay.
			if (amount <= PlebsDontPayThreshold)
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
