using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
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

			var denoms = histogram
				.Where(x => x.Value > 1)
				.OrderByDescending(x => x.Key)
				.Select(x => x.Key)
				.ToArray();

			var inputs = myInputCoins.Select(x => x.EffectiveValue(FeeRate)).ToImmutableArray();
			var totalInput = inputs.Sum();
			var remaining = totalInput;
			var remainingVsize = AvailableVsize;

			List<Money> naiveSet = new();
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

					naiveSet.Add(denom - OutputFee);
					remaining -= denom;
					remainingVsize -= OutputSize;
				}

				if (end)
				{
					break;
				}
			}

			if (remaining >= MinimumAmountPlusFee && remainingVsize >= OutputSize)
			{
				naiveSet.Add(remaining - OutputFee);
			}

			var setCandidates = new Dictionary<int, IEnumerable<Money>>();
			setCandidates.Add(naiveSet.Count, naiveSet);
			var before = DateTimeOffset.UtcNow;
			do
			{
				remaining = inputs.Sum();
				remainingVsize = AvailableVsize;
				var currSet = new List<Money>();
				do
				{
					var denomPlusFees = denoms.Where(x => x <= remaining && x >= (remaining / 3)).ToList();
					var denomPlusFee = denomPlusFees.RandomElement();
					if (denomPlusFee is null || remaining < MinimumAmountPlusFee || remainingVsize < 2 * OutputSize)
					{
						break;
					}

					if (denomPlusFee <= remaining)
					{
						currSet.Add(denomPlusFee - OutputFee);
						remaining -= denomPlusFee;
						remainingVsize -= OutputSize;
					}
				}
				while (currSet.Count <= naiveSet.Count || currSet.Count <= 3);

				if (currSet.Count <= naiveSet.Count || currSet.Count <= 3)
				{
					if (remaining >= MinimumAmountPlusFee && remainingVsize >= OutputSize)
					{
						currSet.Add(remaining - OutputFee);
					}

					setCandidates.TryAdd(currSet.Count, currSet);
				}
			}
			while ((DateTimeOffset.UtcNow - before).TotalMilliseconds <= 100);

			var finalCandidate = setCandidates.RandomElement().Value;

			var totalOutput = finalCandidate.Sum(x => x + OutputFee);
			if (totalOutput > totalInput)
			{
				throw new InvalidOperationException("The decomposer is creating money. Aborting.");
			}
			if (totalOutput + MinimumAmountPlusFee < totalInput)
			{
				throw new InvalidOperationException("The decomposer is losing money. Aborting.");
			}
			return finalCandidate;
		}

		private Dictionary<Money, long> GetDenominationFrequency(IEnumerable<Coin> allInputCoins)
		{
			var secondLargestInput = allInputCoins.OrderByDescending(x => x.Amount).Skip(1).FirstOrDefault();
			IEnumerable<Money> demonsForBreakDown = StandardDenominationsPlusFee.Where(x => secondLargestInput is null || x <= secondLargestInput.EffectiveValue(FeeRate));

			Dictionary<Money, long> denomProbabilities = new();

			foreach (var input in allInputCoins)
			{
				foreach (var denom in BreakDown(input.Amount, demonsForBreakDown))
				{
					if (!denomProbabilities.TryAdd(denom, 1))
					{
						denomProbabilities[denom]++;
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
