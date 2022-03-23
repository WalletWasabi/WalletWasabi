using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Logging;

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

	public IEnumerable<Money> Decompose(IEnumerable<Money> myInputCoinEffectiveValues, IEnumerable<Money> othersInputCoinEffectiveValues)
	{
		var histogram = GetDenominationFrequencies(othersInputCoinEffectiveValues.Concat(myInputCoinEffectiveValues));

		// Filter out and order denominations those have occurred in the frequency table at least twice.
		var preFilteredDenoms = histogram
			.Where(x => x.Value > 1)
			.OrderByDescending(x => x.Key)
			.Select(x => x.Key)
			.ToArray();

		// Filter out denominations very close to each other.
		// Heavy filtering on the top, little to no filtering on the bottom,
		// because in smaller denom levels larger users are expected to participate,
		// but on larger denom levels there's little chance of finding each other.
		var increment = 0.5 / preFilteredDenoms.Length;
		List<ulong> denoms = new();
		var currentLength = preFilteredDenoms.Length;
		foreach (var denom in preFilteredDenoms)
		{
			var filterSeverity = 1 + currentLength * increment;
			if (!denoms.Any() || denom <= (denoms.Last() / filterSeverity))
			{
				denoms.Add(denom);
			}
			currentLength--;
		}

		var myInputs = myInputCoinEffectiveValues.ToArray();
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
		foreach (var denomPlusFee in preFilteredDenoms.Where(x => x <= remaining))
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
		Decomposer.StdDenoms = denoms.Where(x => x <= myInputSum).Select(x => (long)x).ToArray();
		var tolerance = Math.Max(loss, 0.5 * (ulong)MinAllowedOutputAmountPlusFee);
		int maxCount = Math.Min(8, Math.Max(5, naiveSet.Count));

		foreach (var (sum, count, decomp) in Decomposer.Decompose((long)myInputSum, (long)tolerance, maxCount))
		{
			var currentSet = Decomposer.ToRealValuesArray(
				decomp,
				count,
				Decomposer.StdDenoms).Select(Money.Satoshis).ToList();

			hash = new();
			foreach (var item in currentSet.OrderBy(x => x))
			{
				hash.Add(item);
			}
			setCandidates.TryAdd(hash.ToHashCode(), (currentSet, myInputSum - (ulong)currentSet.Sum() + (ulong)count * OutputFee)); // The cost is the remaining + output cost.
		}

		var denomHashSet = preFilteredDenoms.ToHashSet();
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

				// TODO: for debugging purposes, remove later.
				var vsize = finalCandidate.Count() * OutputSize;
				if (finalCandidate.Sum() > myInputSum || vsize > AvailableVsize)
				{
					Logger.LogWarning("The decomposer is creating money. Selecting next candidate.");
					Logger.LogInfo($"Decompose: '{myInputSum}', '{tolerance}', '{maxCount}'.");
					Logger.LogInfo($"StdDenoms: '{string.Join(" ", Decomposer.StdDenoms)}'.");
					Logger.LogInfo($"FinalCandidate: '{string.Join(" ", finalCandidate)}'.");
					Logger.LogInfo($"AvailableVsize: '{vsize}', '{AvailableVsize}'.");
					continue;
				}

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
	private Dictionary<ulong, long> GetDenominationFrequencies(IEnumerable<Money> inputEffectiveValues)
	{
		var secondLargestInput = inputEffectiveValues.OrderByDescending(x => x).Skip(1).First();
		IEnumerable<ulong> demonsForBreakDown = DenominationsPlusFees.Where(x => x <= (ulong)secondLargestInput.Satoshi);

		Dictionary<ulong, long> denomFrequencies = new();
		foreach (var input in inputEffectiveValues)
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
	private IEnumerable<ulong> BreakDown(Money coininputEffectiveValue, IEnumerable<ulong> denominations)
	{
		var remaining = coininputEffectiveValue;

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
