using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.DecompositionAlgs
{
	public class GreedyDecomposer
	{
		public Decomposition Decomposition { get; }
		private Money DustThreshold { get; }
		private FeeRate FeeRate { get; }

		public GreedyDecomposer(IEnumerable<Money> preferredDenominations, Money dustThreshold, FeeRate feeRate)
		{
			Decomposition = new(preferredDenominations);
			DustThreshold = dustThreshold;
			FeeRate = feeRate;
		}

		public void Decompose(Coin coin)
		{
			Money remaining = coin.Amount - FeeRate.GetFee(coin.ScriptPubKey.EstimateOutputVsize());

			while (remaining > DustThreshold)
			{
				Money? denom = LargestDenomBelowIncl(remaining);
				if (denom is null || denom == Money.Zero)
				{
					break;
				}
				Decomposition.Extend(denom);

				var effectiveCost = coin.Amount + FeeRate.GetFee(coin.ScriptPubKey.EstimateOutputVsize());

				remaining -= effectiveCost;
			}

			if (remaining > DustThreshold)
			{
				Decomposition.Extend(remaining);
			}
		}

		private Money? LargestDenomBelowIncl(Money amount)
		{
			Money result = Decomposition.First();
			if (result > amount)
			{
				return null;
			}

			foreach (var coin in Decomposition)
			{
				if (coin <= amount)
				{
					result = coin;
				}
				else
				{
					break;
				}
			}
			return result;
		}
	}
}
