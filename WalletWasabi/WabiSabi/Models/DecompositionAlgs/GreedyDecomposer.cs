using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.DecompositionAlgs
{
	public class GreedyDecomposer
	{
		public GreedyDecomposer(IEnumerable<Money> preferredDenominations, Money dustThreshold, FeeRate feeRate)
		{
			Decomposition = new(preferredDenominations);
			DustThreshold = dustThreshold;
			FeeRate = feeRate;
		}

		public Decomposition Decomposition { get; }
		private Money DustThreshold { get; }
		private FeeRate FeeRate { get; }

		public void Decompose(Coin coin)
		{
			Money remaining = coin.Amount - FeeRate.GetFee(coin.ScriptPubKey.EstimateOutputVsize());

			while (remaining > DustThreshold)
			{
				if (!TryGetLargestDenomBelowIncl(remaining, out var denom))
				{
					break;
				}
				Decomposition.AddSorted(denom);

				var effectiveCost = coin.Amount + FeeRate.GetFee(coin.ScriptPubKey.EstimateOutputVsize());

				remaining -= effectiveCost;
			}

			if (remaining > DustThreshold)
			{
				Decomposition.AddSorted(remaining);
			}
		}

		private bool TryGetLargestDenomBelowIncl(Money amount, [NotNullWhen(true)] out Money? result)
		{
			result = Decomposition.Where(denoms => denoms <= amount).LastOrDefault();
			return result is not null;
		}
	}
}
