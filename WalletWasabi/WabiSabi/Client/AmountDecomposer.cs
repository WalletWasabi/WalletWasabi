using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.Helpers;
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
			var histogram = GetDenominationFrequency(allInputCoins);

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

		private Dictionary<Money, long> GetDenominationFrequency(IEnumerable<Coin> allInputCoins)
		{
			IEnumerable<Money> fakeUsersAmounts = allInputCoins
				.Select(x => x.EffectiveValue(FeeRate))
				.CombinationsWithoutRepetition(1, 4)
				.Select(x => x.Sum())
				.Where(x => x < Constants.MaximumNumberOfSatoshis)
				.ToImmutableArray();
			var secondLargestInput = fakeUsersAmounts.OrderByDescending(x => x).Skip(1).FirstOrDefault();
			IEnumerable<Money> demonsForBreakDown = StandardDenominationsPlusFee.Where(x => secondLargestInput is null || x <= secondLargestInput);

			Dictionary<Money, long> denomProbabilities = new();

			foreach (var input in fakeUsersAmounts)
			{
				foreach (var denom in BreakDown(input, demonsForBreakDown))
				{
					if (!denomProbabilities.TryAdd(denom, 1))
					{
						denomProbabilities[denom] += 1;
					}
				}
			}

			return denomProbabilities;
		}

		private IEnumerable<Money> BreakDown(long amount, IEnumerable<Money> denominations)
		{
			var remaining = amount;

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
