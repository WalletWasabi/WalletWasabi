using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.Decomposition;

namespace WalletWasabi.WabiSabi.Client
{
	public class AmountDecomposer
	{
		public AmountDecomposer(FeeRate feeRate, MoneyRange allowedOutputAmount, int outputSize)
		{
			OutputSize = outputSize;
			FeeRate = feeRate;
			MinimumAmountPlusFee = allowedOutputAmount.Min + OutputFee;
			StandardDenominationsPlusFee = StandardDenomination.Values
				.Select(x => x + OutputFee)
				.OrderByDescending(x => x)
				.ToImmutableArray();
		}

		public FeeRate FeeRate { get; }

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

			foreach (var denom in denoms.Where(x => x <= remaining))
			{
				if (remaining < MinimumAmountPlusFee)
				{
					break;
				}

				while (denom <= remaining)
				{
					outputAmounts.Add(denom);
					remaining -= denom;
				}
			}

			if (remaining >= MinimumAmountPlusFee)
			{
				outputAmounts.Add(remaining);
			}

			return outputAmounts.Select(x => x - OutputFee);
		}

		private Dictionary<Money, uint> GetDenominationProbabilities(IEnumerable<Coin> allInputCoins)
		{
			var secondLargestInput = allInputCoins.OrderByDescending(x => x.Amount).Skip(1).FirstOrDefault();
			IEnumerable<Money> demonsForBreakDown = StandardDenominationsPlusFee.Where(x => secondLargestInput is null || x <= secondLargestInput.EffectiveValue(FeeRate));

			Dictionary<Money, uint> denomProbabilities = new();

			foreach (var input in allInputCoins)
			{
				foreach (var denom in BreakDown(input, demonsForBreakDown))
				{
					if (!denomProbabilities.TryAdd(denom, 1))
					{
						denomProbabilities[denom]++;
					}
				}
			}

			return denomProbabilities;
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
