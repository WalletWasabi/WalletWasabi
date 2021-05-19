using NBitcoin;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.DecompositionAlgs
{
	public class GreedyDecomposer
	{
		private Decomposition Decomposition { get; } = new();

		public GreedyDecomposer()
		{
			Decomposition.Extend(new Coin() { Amount = Money.Zero });
		}

		public virtual Money? PickNextAmount(Money remaining, Money dustThreshold)
		{
			return LargestDenomBelowIncl(remaining)?.Amount;
		}

		public void Decompose(Money amount, Money dustThreshold, FeeRate feeRate)
		{
			Money remaining = amount - feeRate.GetFee(Coin.OutputVbytes);

			while (remaining > dustThreshold)
			{
				Money? denom = PickNextAmount(remaining, dustThreshold);
				if (denom is null || denom == Money.Zero)
				{
					break;
				}
				Coin coin = new() { Amount = denom };
				Decomposition.Extend(coin);
				remaining -= coin.EffectiveCost(feeRate);
			}

			if (remaining > dustThreshold)
			{
				Coin coin = new() { Amount = remaining };
				Decomposition.Extend(coin);
			}
		}

		private Coin? LargestDenomBelowIncl(Money amount)
		{
			Coin result = Decomposition.First();
			foreach (var coin in Decomposition)
			{
				if (coin.Amount <= amount)
				{
					result = new Coin() { Amount = amount };
				}
			}
			return result;
		}
	}
}
