using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.Decomposition;

namespace WalletWasabi.WabiSabi.Client
{
	public class AmountDecomposer
	{
		public AmountDecomposer(FeeRate feeRate, Money minAllowedOutputAmount, int outputSize, int availableVsize)
		{
			OutputSize = outputSize;
			FeeRate = feeRate;
			AvailableVsize = availableVsize;
			MinimumAmountPlusFee = minAllowedOutputAmount + OutputFee;
			StandardDenominationsPlusFee = StandardDenomination.Values
				.Select(x => x + OutputFee)
				.OrderByDescending(x => x)
				.ToImmutableArray();
		}

		public FeeRate FeeRate { get; }
		public int AvailableVsize { get; }
		public Money MinimumAmountPlusFee { get; }
		public Money OutputFee => FeeRate.GetFee(OutputSize);
		public int OutputSize { get; }
		private ImmutableArray<Money> StandardDenominationsPlusFee { get; }

		public IEnumerable<Money> Decompose(IEnumerable<Coin> myInputCoins, IEnumerable<Coin> allInputCoins)
		{
			var histogram = GetDenominationProbabilities(allInputCoins);

			var inputs = myInputCoins.Select(x => x.EffectiveValue(FeeRate));
			var remaining = inputs.Sum();

			var denoms = histogram
				.OrderByDescending(x => x.Key)
				.Where(x => x.Value > 1)
				.Select(x => x.Key)
				.ToArray();

			List<Money> outputAmounts = new();
			var remainingVsize = AvailableVsize;

			bool end = false;
			foreach (var denom in denoms.Where(x => x <= remaining))
			{
				while (denom <= remaining)
				{
					if (remaining < MinimumAmountPlusFee || remainingVsize < 2 * OutputSize)
					{
						end = true;
						break;
					}

					outputAmounts.Add(denom - OutputFee);
					remaining -= denom;
					remainingVsize -= OutputSize;
				}

				if (end)
				{
					break;
				}
			}

			if (remaining >= MinimumAmountPlusFee)
			{
				outputAmounts.Add(remaining - OutputFee);
			}

			return outputAmounts;
		}

		private Dictionary<Money, long> GetDenominationProbabilities(IEnumerable<Coin> allInputCoins)
		{
			var secondLargestInput = allInputCoins.OrderByDescending(x => x.Amount).Skip(1).FirstOrDefault();
			IEnumerable<Money> demonsForBreakDown = StandardDenominationsPlusFee.Where(x => secondLargestInput is null || x <= secondLargestInput.EffectiveValue(FeeRate));

			Dictionary<Money, long> denomProbabilities = new();

			foreach (var input in allInputCoins)
			{
				foreach (var denom in BreakDown(input, demonsForBreakDown))
				{
					var weight = Weight(denom.Satoshi);

					if (!denomProbabilities.TryAdd(denom, weight))
					{
						denomProbabilities[denom] += weight;
					}
				}
			}

			return denomProbabilities;
		}

		private long Weight(long val)
		{
			// Bias denom selection as the square of the value.
			return val * val;
		}

		private IEnumerable<Money> BreakDown(Coin coin, IEnumerable<Money> denominations)
		{
			var remaining = coin.EffectiveValue(FeeRate);

			foreach (var denomPlusFee in denominations)
			{
				if (denomPlusFee < MinimumAmountPlusFee || remaining < MinimumAmountPlusFee)
				{
					break;
				}

				while (denomPlusFee <= remaining)
				{
					yield return denomPlusFee;
					remaining -= denomPlusFee;
				}
			}

			if (remaining >= MinimumAmountPlusFee)
			{
				yield return remaining;
			}
		}
	}
}
