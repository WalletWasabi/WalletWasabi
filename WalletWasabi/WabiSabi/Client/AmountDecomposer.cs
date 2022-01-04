using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Client
{
	/// <summary>
	/// Pull requests to this file must be up to date with this simulation to ensure correctness: https://github.com/nopara73/Sake
	/// </summary>
	public class AmountDecomposer
	{
		/// <param name="feeRate">Bitcoin network fee rate the coinjoin is targeting.</param>
		/// <param name="minAllowedOutputAmount"></param>
		/// <param name="outputSize">Size of an output.</param>
		/// <param name="availableVsize">Available virtual size for outputs.</param>
		public AmountDecomposer(FeeRate feeRate, Money minAllowedOutputAmount, int outputSize, int availableVsize)
		{
			FeeRate = feeRate;
			OutputSize = outputSize;
			AvailableVsize = availableVsize;

			MinAllowedOutputAmountPlusFee = minAllowedOutputAmount + OutputFee;

			// Create many standard denominations.
			DenominationsPlusFees = CreateDenominationsPlusFees();
		}

		public FeeRate FeeRate { get; }
		public int AvailableVsize { get; }
		public Money MinAllowedOutputAmountPlusFee { get; }
		public Money OutputFee => FeeRate.GetFee(OutputSize);
		public int OutputSize { get; }
		public IOrderedEnumerable<ulong> DenominationsPlusFees { get; }

		private IOrderedEnumerable<ulong> CreateDenominationsPlusFees()
		{
			ulong maxSatoshis = ProtocolConstants.MaxAmountPerAlice;
			ulong minSatoshis = MinAllowedOutputAmountPlusFee;
			var denominations = new HashSet<ulong>();

			// Powers of 2
			for (int i = 0; i < int.MaxValue; i++)
			{
				var denom = (ulong)Math.Pow(2, i) + OutputFee;

				if (denom < minSatoshis)
				{
					continue;
				}

				if (denom > maxSatoshis)
				{
					break;
				}

				denominations.Add(denom);
			}

			// Powers of 3
			for (int i = 0; i < int.MaxValue; i++)
			{
				var denom = (ulong)Math.Pow(3, i) + OutputFee;

				if (denom < minSatoshis)
				{
					continue;
				}

				if (denom > maxSatoshis)
				{
					break;
				}

				denominations.Add(denom);
			}

			// Powers of 3 * 2
			for (int i = 0; i < int.MaxValue; i++)
			{
				var denom = (ulong)Math.Pow(3, i) * 2 + OutputFee;

				if (denom < minSatoshis)
				{
					continue;
				}

				if (denom > maxSatoshis)
				{
					break;
				}

				denominations.Add(denom);
			}

			// Powers of 10 (1-2-5 series)
			for (int i = 0; i < int.MaxValue; i++)
			{
				var denom = (ulong)Math.Pow(10, i) + OutputFee;

				if (denom < minSatoshis)
				{
					continue;
				}

				if (denom > maxSatoshis)
				{
					break;
				}

				denominations.Add(denom);
			}

			// Powers of 10 * 2 (1-2-5 series)
			for (int i = 0; i < int.MaxValue; i++)
			{
				var denom = (ulong)Math.Pow(10, i) * 2 + OutputFee;

				if (denom < minSatoshis)
				{
					continue;
				}

				if (denom > maxSatoshis)
				{
					break;
				}

				denominations.Add(denom);
			}

			// Powers of 10 * 5 (1-2-5 series)
			for (int i = 0; i < int.MaxValue; i++)
			{
				var denom = (ulong)Math.Pow(10, i) * 5 + OutputFee;

				if (denom < minSatoshis)
				{
					continue;
				}

				if (denom > maxSatoshis)
				{
					break;
				}

				denominations.Add(denom);
			}

			return denominations.OrderByDescending(x => x);
		}

		public IEnumerable<Money> Decompose(IEnumerable<Coin> myInputCoins, IEnumerable<Coin> othersInputCoins)
		{
			var histogram = GetDenominationFrequencies(othersInputCoins.Concat(myInputCoins));

			// Filter out and order denominations those have occured in the frequency table at least twice.
			var denoms = histogram
				.Where(x => x.Value > 1)
				.OrderByDescending(x => x.Key)
				.Select(x => x.Key)
				.ToArray();

			var myInputs = myInputCoins.Select(x => x.EffectiveValue(FeeRate)).ToArray();
			var myInputSum = myInputs.Sum();
			var remaining = myInputSum;
			var remainingVsize = AvailableVsize;

			var setCandidates = new Dictionary<ulong, (IEnumerable<Money> Decomp, Money Cost)>();
			var random = new Random();

			// How many times can we participate with the same denomination.
			var maxDenomUsage = random.Next(2, 8);

			// Create the most naive decomposition for starter.
			List<Money> naiveSet = new();
			bool end = false;
			foreach (var denomPlusFee in denoms.Where(x => x <= remaining))
			{
				var denomUsage = 0;
				while (denomPlusFee <= remaining)
				{
					// We can only let this go forward if at least 2 output can be added (denom + potential change)
					if (remaining < MinAllowedOutputAmountPlusFee || remainingVsize < 2 * OutputSize)
					{
						end = true;
						break;
					}

					naiveSet.Add(denomPlusFee);
					remaining -= denomPlusFee;
					remainingVsize -= OutputSize;
					denomUsage++;

					// If we reached the limit, the rest will be change.
					if (denomUsage >= maxDenomUsage)
					{
						end = true;
						break;
					}
				}

				if (end)
				{
					break;
				}
			}

			var loss = 0UL;
			if (remaining >= MinAllowedOutputAmountPlusFee)
			{
				naiveSet.Add(remaining);
			}
			else
			{
				// This goes to miners.
				loss = remaining;
			}

			// This can happen when smallest denom is larger than the input sum.
			if (naiveSet.Count == 0)
			{
				naiveSet.Add(remaining);
			}

			setCandidates.Add(
				naiveSet.OrderBy(x => x).Aggregate((x, y) => 31 * x + y), // Create hash to ensure uniqueness.
				(naiveSet, loss + (ulong)naiveSet.Count * OutputFee)); // The cost is the remaining + output cost.

			// Create many decompositions for optimization.
			var before = DateTimeOffset.UtcNow;
			do
			{
				remaining = myInputs.Sum();
				remainingVsize = AvailableVsize;
				var currSet = new List<Money>();
				do
				{
					var denomPlusFees = denoms.Where(x => x <= remaining && x >= (remaining / 3)).ToList();
					if (denomPlusFees.Any())
					{
						break;
					}

					var denomPlusFee = denomPlusFees.RandomElement();
					if (remaining < MinAllowedOutputAmountPlusFee || remainingVsize < 2 * OutputSize)
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
					if (remaining >= MinAllowedOutputAmountPlusFee)
					{
						currSet.Add(remaining - OutputFee);
					}

					setCandidates.TryAdd(currSet.Count, currSet);
				}
			}
			while ((DateTimeOffset.UtcNow - before).TotalMilliseconds <= 100);

			var rand = new Random();
			var counts = setCandidates.Select(x => x.Key).Distinct().OrderBy(x => x);
			var selectedCount = counts.Max();
			foreach (var count in counts)
			{
				if (rand.NextDouble() < 0.3)
				{
					selectedCount = count;
					break;
				}
			}

			var finalCandidate = setCandidates.Where(x => x.Key == selectedCount).RandomElement().Value;

			var totalOutputAmount = finalCandidate.Sum(x => x + OutputFee);
			if (totalOutputAmount > myInputSum)
			{
				throw new InvalidOperationException("The decomposer is creating money. Aborting.");
			}
			if (totalOutputAmount + MinAllowedOutputAmountPlusFee < myInputSum)
			{
				throw new InvalidOperationException("The decomposer is losing money. Aborting.");
			}

			var totalOutputVsize = finalCandidate.Count() * OutputSize;
			if (totalOutputVsize > AvailableVsize)
			{
				throw new InvalidOperationException("The decomposer created more outputs than it can. Aborting.");
			}
			return finalCandidate;
		}

		/// <returns>Pair of denomination and the number of times we found it in a breakdown.</returns>
		private Dictionary<ulong, long> GetDenominationFrequencies(IEnumerable<Coin> inputs)
		{
			var secondLargestInput = inputs.OrderByDescending(x => x.Amount).Skip(1).First();
			IEnumerable<ulong> demonsForBreakDown = DenominationsPlusFees.Where(x => x <= (ulong)secondLargestInput.EffectiveValue(FeeRate).Satoshi);

			Dictionary<ulong, long> denomFrequencies = new();
			foreach (var input in inputs)
			{
				foreach (var denom in BreakDown(input, demonsForBreakDown))
				{
					if (!denomFrequencies.TryAdd(denom, 1))
					{
						denomFrequencies[denom]++;
					}
				}
			}

			return denomFrequencies;
		}

		/// <summary>
		/// Greedily decomposes an amount to the given denominations.
		/// </summary>
		private IEnumerable<ulong> BreakDown(Coin coin, IEnumerable<ulong> denominations)
		{
			var remaining = coin.EffectiveValue(FeeRate);

			foreach (var denomPlusFee in denominations)
			{
				if (denomPlusFee < MinAllowedOutputAmountPlusFee || remaining < MinAllowedOutputAmountPlusFee)
				{
					break;
				}

				while (denomPlusFee <= remaining)
				{
					yield return denomPlusFee;
					remaining -= denomPlusFee;
				}
			}

			if (remaining >= MinAllowedOutputAmountPlusFee)
			{
				yield return remaining;
			}
		}
	}
}
