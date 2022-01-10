using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Client;

/// <summary>
/// Pull requests to this file must be up to date with this simulation to ensure correctness: https://github.com/nopara73/Sake
/// </summary>
public class AmountDecomposer
{
	/// <param name="feeRate">Bitcoin network fee rate the coinjoin is targeting.</param>
	/// <param name="minAllowedOutputAmount">Minimum output amount that's allowed to be registered.</param>
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

		var setCandidates = new Dictionary<int, (IEnumerable<Money> Decomp, Money Cost)>();
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

		HashCode hash = new();
		foreach (var item in naiveSet.OrderBy(x => x))
		{
			hash.Add(item);
		}

		setCandidates.Add(
			hash.ToHashCode(), // Create hash to ensure uniqueness.
			(naiveSet, loss + (ulong)naiveSet.Count * OutputFee)); // The cost is the remaining + output cost.

		// Create many decompositions for optimization.
		var before = DateTimeOffset.UtcNow;
		do
		{
			var currSet = new List<Money>();
			remaining = myInputs.Sum();
			remainingVsize = AvailableVsize;
			do
			{
				var denomPlusFees = denoms.Where(x => x <= remaining && x >= (remaining / 3)).ToList();
				if (!denomPlusFees.Any())
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
					currSet.Add(denomPlusFee);
					remaining -= denomPlusFee;
					remainingVsize -= OutputSize;
				}
			}
			while (currSet.Count <= naiveSet.Count || currSet.Count <= 3);

			// If currSet.Count <= 3 then we still generate sets to add ambiguity.
			if (currSet.Count <= naiveSet.Count || currSet.Count <= 3)
			{
				loss = 0;
				if (remaining >= MinAllowedOutputAmountPlusFee)
				{
					currSet.Add(remaining);
				}
				else
				{
					loss = remaining;
				}

				// When not even the minimum denom is reached.
				if (currSet.Count == 0)
				{
					currSet.Add(remaining);
					loss = 0;
				}

				hash = new();
				foreach (var item in currSet.OrderBy(x => x))
				{
					hash.Add(item);
				}

				setCandidates.TryAdd(
					hash.ToHashCode(),
					(currSet, loss + (ulong)currSet.Count * OutputFee));
			}
		}
		while ((DateTimeOffset.UtcNow - before).TotalMilliseconds <= 500);

		var denomHashSet = denoms.ToHashSet();
		var finalCandidates = setCandidates.Select(x => x.Value).ToList();
		finalCandidates.Shuffle();

		var orderedCandidates = finalCandidates
			.OrderBy(x => x.Cost) // Less cost is better.
			.ThenBy(x => x.Decomp.All(x => denomHashSet.Contains(x)) ? 0 : 1) // Prefer no change.
			.Select(x => x).ToList();

		var finalCandidate = orderedCandidates.First().Decomp;
		foreach (var candidate in orderedCandidates)
		{
			if (random.NextDouble() < 0.5)
			{
				finalCandidate = candidate.Decomp;
				break;
			}
		}

		finalCandidate = finalCandidate.Select(x => x - OutputFee);

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
