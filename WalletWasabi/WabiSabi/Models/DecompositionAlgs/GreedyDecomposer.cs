using NBitcoin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.DecompositionAlgs
{
	public class GreedyDecomposer
	{
		public GreedyDecomposer(IEnumerable<Money> baseDenominations, Money dustThreshold, FeeRate feeRate)
		{
			BaseDenominations = baseDenominations.OrderBy(d => d).ToArray();
			DustThreshold = dustThreshold;
			FeeRate = feeRate;
		}

		private Money[] BaseDenominations { get; }

		private List<Money> Decomposition { get; set; } = new List<Money>();
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

				Decomposition.InsertSorted(denom);

				var effectiveCost = coin.Amount + FeeRate.GetFee(coin.ScriptPubKey.EstimateOutputVsize());

				remaining -= effectiveCost;
			}

			if (remaining > DustThreshold)
			{
				Decomposition.InsertSorted(remaining);
			}
		}

		private bool TryGetLargestDenomBelowIncl(Money amount, [NotNullWhen(true)] out Money? result)
		{
			result = BaseDenominations.Where(denoms => denoms <= amount).LastOrDefault();
			return result is not null;
		}

		public ImmutableArray<Money> GetDecomposition()
		{
			return Decomposition.ToImmutableArray();
		}
	}
}
