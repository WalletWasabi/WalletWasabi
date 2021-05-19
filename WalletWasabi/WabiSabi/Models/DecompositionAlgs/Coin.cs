using NBitcoin;
using System;

namespace WalletWasabi.WabiSabi.Models.DecompositionAlgs
{
	public record Coin : IComparable<Coin>
	{
		public Money Amount { get; init; } = Money.Zero;

		public const int OutputVbytes = 31;
		public const decimal InputVbytes = 68.5m;

		private int HashCode { get; } = Guid.NewGuid().GetHashCode();

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

		public int CompareTo(Coin? other)
		{
			if (other is Coin coin)
			{
				return Amount.CompareTo(coin.Amount);
			}
			throw new NullReferenceException();
		}
	}
}
