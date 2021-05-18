using NBitcoin;
using System;

namespace WalletWasabi.WabiSabi.Models.DecompositionAlgs
{
	public record Coin : IComparable
	{
		public Money Amount { get; init; } = Money.Zero;
		public const int OutputVbytes = 31;
		public const decimal InputVbytes = 68.5m;

		private int HashCode { get; } = new Guid().GetHashCode();

		public Money EffectiveValue(FeeRate feeRate)
		{
			long sats = (long)Math.Floor(feeRate.SatoshiPerByte * InputVbytes);
			return Amount - Money.Satoshis(sats);
		}

		public Money EffectiveCost(FeeRate feeRate)
		{
			long sats = feeRate.GetFee(OutputVbytes);
			return Amount + Money.Satoshis(sats);
		}

		public override int GetHashCode()
		{
			return HashCode;
		}

		public int CompareTo(object? obj)
		{
			if (obj is Money money)
			{
				return CompareTo(money);
			}
			throw new NullReferenceException();
		}
	}
}
