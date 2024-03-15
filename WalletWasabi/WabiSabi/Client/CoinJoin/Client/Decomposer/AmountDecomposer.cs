using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

/// <summary>
/// Pull requests to this file must be up to date with this simulation to ensure correctness: https://github.com/nopara73/Sake
/// </summary>
public class AmountDecomposer
{
	/// <param name="feeRate">Bitcoin network fee rate the coinjoin is targeting.</param>
	/// <param name="minAllowedOutputAmount">Min output amount that's allowed to be registered.</param>
	/// <param name="maxAllowedOutputAmount">Max output amount that's allowed to be registered.</param>
	/// <param name="availableVsize">Available virtual size for outputs.</param>
	/// <param name="random">Allows testing by setting a seed value for the random number generator.</param>
	public AmountDecomposer(FeeRate feeRate, Money minAllowedOutputAmount, Money maxAllowedOutputAmount, int availableVsize, IEnumerable<ScriptType> allowedOutputTypes, WasabiRandom random)
	{
		FeeRate = feeRate;

		AvailableVsize = availableVsize;
		AllowedOutputTypes = allowedOutputTypes;
		MinAllowedOutputAmount = minAllowedOutputAmount;
		MaxAllowedOutputAmount = maxAllowedOutputAmount;
		Random = random;

		// Create many standard denominations.
		Denominations = DenominationBuilder.CreateDenominations(MinAllowedOutputAmount, MaxAllowedOutputAmount, FeeRate, AllowedOutputTypes, random);

		ChangeScriptType = AllowedOutputTypes.RandomElement(random);
	}

	public FeeRate FeeRate { get; }
	public int AvailableVsize { get; }
	public IEnumerable<ScriptType> AllowedOutputTypes { get; }
	public Money MinAllowedOutputAmount { get; }
	public Money MaxAllowedOutputAmount { get; }

	public IOrderedEnumerable<Output> Denominations { get; }
	public ScriptType ChangeScriptType { get; }
	public Money ChangeFee => FeeRate.GetFee(ChangeScriptType.EstimateOutputVsize());
	private WasabiRandom Random { get; }

	private IEnumerable<Output> GetFilteredDenominations(IEnumerable<Money> allInputEffectiveValues)
	{
		var histogram = GetDenominationFrequencies(allInputEffectiveValues);

		// Filter out and order denominations those have occurred in the frequency table at least twice.
		var preFilteredDenoms = histogram
			.Where(x => x.Value > 1)
			.OrderByDescending(x => x.Key.EffectiveCost)
			.Select(x => x.Key)
			.ToArray();

		// Filter out denominations very close to each other.
		// Heavy filtering on the top, little to no filtering on the bottom,
		// because in smaller denom levels larger users are expected to participate,
		// but on larger denom levels there's little chance of finding each other.
		var increment = 0.5 / preFilteredDenoms.Length;
		List<Output> denoms = new();
		var currentLength = preFilteredDenoms.Length;
		foreach (var denom in preFilteredDenoms)
		{
			var filterSeverity = 1 + currentLength * increment;
			if (denoms.Count == 0 || denom.Amount.Satoshi <= (long)(denoms.Last().Amount.Satoshi / filterSeverity))
			{
				denoms.Add(denom);
			}
			currentLength--;
		}

		return denoms;
	}

	public IEnumerable<Output> Decompose(IEnumerable<Money> myInputCoinEffectiveValues, IEnumerable<Money> othersInputCoinEffectiveValues)
	{
		var denoms = GetFilteredDenominations(othersInputCoinEffectiveValues.Concat(myInputCoinEffectiveValues));
		var myInputs = myInputCoinEffectiveValues.ToArray();
		var myInputSum = myInputs.Sum();
		var smallestScriptType = Math.Min(ScriptType.P2WPKH.EstimateOutputVsize(), ScriptType.Taproot.EstimateOutputVsize());
		var maxNumberOfOutputsAllowed = Math.Min(AvailableVsize / smallestScriptType, 10); // The absolute max possible with the smallest script type.

		// If there are no output denominations, the participation in coinjoin makes no sense.
		if (!denoms.Any())
		{
			throw new InvalidOperationException(
				"No valid output denominations found. This can occur when an insufficient number of coins are registered to participate in the coinjoin.");
		}

		// If my input sum is smaller than the smallest denomination, then participation in a coinjoin makes no sense.
		if (denoms.Min(x => x.EffectiveCost) > myInputSum)
		{
			throw new InvalidOperationException("Not enough coins registered to participate in the coinjoin.");
		}

		var setCandidates = new Dictionary<int, (IEnumerable<Output> Decomposition, Money Cost)>();

		// Create the most naive decomposition for starter.
		var naiveDecomp = CreateNaiveDecomposition(denoms, myInputSum, maxNumberOfOutputsAllowed);
		setCandidates.Add(naiveDecomp.Key, naiveDecomp.Value);

		// Create more pre-decompositions for sanity.
		var preDecomps = CreatePreDecompositions(denoms, myInputSum, maxNumberOfOutputsAllowed);
		foreach (var decomp in preDecomps)
		{
			setCandidates.TryAdd(decomp.Key, decomp.Value);
		}

		// Create many decompositions for optimization.
		var changelessDecomps = CreateChangelessDecompositions(denoms, myInputSum, maxNumberOfOutputsAllowed);
		foreach (var decomp in changelessDecomps)
		{
			setCandidates.TryAdd(decomp.Key, decomp.Value);
		}

		var denomHashSet = denoms.ToHashSet();
		var preCandidates = setCandidates.Select(x => x.Value).ToList();

		// If there are changeless candidates, don't even consider ones with change.
		var changelessCandidates = preCandidates.Where(x => x.Decomposition.All(y => denomHashSet.Contains(y))).ToList();
		var changeAvoided = changelessCandidates.Count != 0;
		if (changeAvoided)
		{
			preCandidates = changelessCandidates;
		}
		preCandidates.Shuffle(Random);

		var orderedCandidates = preCandidates
			.OrderBy(x => x.Decomposition.Sum(y => denomHashSet.Contains(y) ? Money.Zero : y.Amount)) // Less change is better.
			.ThenBy(x => x.Cost) // Less cost is better.
			.ThenBy(x => x.Decomposition.Any(d => d.ScriptType == ScriptType.Taproot) && x.Decomposition.Any(d => d.ScriptType == ScriptType.P2WPKH) ? 0 : 1) // Prefer mixed scripts types.
			.Select(x => x).ToList();

		// We want to introduce randomness between the best selections.
		// If we successfully avoided change, then what matters is cost,
		// if we didn't then cost calculation is irrelevant, because the size of change is more costly.
		(IEnumerable<Output> Decomp, Money Cost)[] finalCandidates;
		if (changeAvoided)
		{
			var bestCandidateCost = orderedCandidates.First().Cost;
			var costTolerance = Money.Coins(bestCandidateCost.ToUnit(MoneyUnit.BTC) * 1.2m);
			finalCandidates = orderedCandidates.Where(x => x.Cost <= costTolerance).ToArray();
		}
		else
		{
			// Change can only be max between: 100.000 satoshis, 10% of the inputs sum or 20% more than the best candidate change
			var bestCandidateChange = FindChange(orderedCandidates.First().Decomposition, denomHashSet);
			var changeTolerance = Money.Coins(
				Math.Max(
					Math.Max(
						myInputSum.ToUnit(MoneyUnit.BTC) * 0.1m,
						bestCandidateChange.ToUnit(MoneyUnit.BTC) * 1.2m),
					Money.Satoshis(100000).ToUnit(MoneyUnit.BTC)));

			finalCandidates = orderedCandidates.Where(x => FindChange(x.Decomposition, denomHashSet) <= changeTolerance).ToArray();
		}

		// We want to make sure our random selection is not between similar decompositions.
		// Different largest elements result in very different decompositions.
		var largestAmount = finalCandidates.Select(x => x.Decomp.First()).ToHashSet().RandomElement(Random);
		var finalCandidate = finalCandidates.Where(x => x.Decomp.First() == largestAmount).RandomElement(Random).Decomp;

		var totalOutputAmount = Money.Satoshis(finalCandidate.Sum(x => x.EffectiveCost));
		if (totalOutputAmount > myInputSum)
		{
			throw new InvalidOperationException("The decomposer is creating money. Aborting.");
		}
		if (totalOutputAmount + MinAllowedOutputAmount + ChangeFee < myInputSum)
		{
			throw new InvalidOperationException("The decomposer is losing money. Aborting.");
		}

		var totalOutputVsize = finalCandidate.Sum(d => d.ScriptType.EstimateOutputVsize());
		if (totalOutputVsize > AvailableVsize)
		{
			throw new InvalidOperationException("The decomposer created more outputs than it can. Aborting.");
		}
		return finalCandidate;
	}

	private static Money FindChange(IEnumerable<Output> decomposition, HashSet<Output> denomHashSet)
	{
		return decomposition.Sum(x => denomHashSet.Contains(x) ? Money.Zero : x.Amount);
	}

	private IDictionary<int, (IEnumerable<Output> Decomp, Money Cost)> CreateChangelessDecompositions(IEnumerable<Output> denoms, Money myInputSum, int maxNumberOfOutputsAllowed)
	{
		var setCandidates = new Dictionary<int, (IEnumerable<Output> Decomp, Money Cost)>();

		var stdDenoms = denoms.Select(d => d.EffectiveCost.Satoshi).Where(x => x <= myInputSum.Satoshi).ToArray();

		if (maxNumberOfOutputsAllowed > 1)
		{
			foreach (var (sum, count, decomp) in Decomposer.Decompose(
				target: (long)myInputSum,
				tolerance: MinAllowedOutputAmount + ChangeFee,
				maxCount: Math.Min(maxNumberOfOutputsAllowed, 8), // Decomposer doesn't do more than 8.
				stdDenoms: stdDenoms))
			{
				var currentSet = Decomposer.ToRealValuesArray(
					decomp,
					count,
					stdDenoms).Select(Money.Satoshis).ToList();

				// Translate back to denominations.
				List<Output> finalDenoms = new();
				foreach (var outputPlusFee in currentSet)
				{
					finalDenoms.Add(denoms.First(d => d.EffectiveCost == outputPlusFee));
				}

				// The decomposer won't take vsize into account for different script types, checking it back here if too much, disregard the decomposition.
				var totalVSize = finalDenoms.Sum(d => d.ScriptType.EstimateOutputVsize());
				if (totalVSize > AvailableVsize)
				{
					continue;
				}

				var deficit = myInputSum - (ulong)finalDenoms.Sum(d => d.EffectiveCost) + CalculateCost(finalDenoms);

				setCandidates.TryAdd(CalculateHash(finalDenoms), (finalDenoms, deficit));
			}
		}

		return setCandidates;
	}

	private IDictionary<int, (IEnumerable<Output> Decomp, Money Cost)> CreatePreDecompositions(IEnumerable<Output> denoms, Money myInputSum, int maxNumberOfOutputsAllowed)
	{
		var setCandidates = new Dictionary<int, (IEnumerable<Output> Decomp, Money Cost)>();

		for (int i = 0; i < 10_000; i++)
		{
			var remainingVsize = AvailableVsize;
			var remaining = myInputSum;
			List<Output> currentSet = new();
			while (true)
			{
				var denom = denoms.Where(x => x.EffectiveCost <= remaining && x.EffectiveCost >= remaining / 3).RandomElement(Random)
					?? denoms.FirstOrDefault(x => x.EffectiveCost <= remaining);

				// Continue only if there is enough remaining amount and size to create one output (+ change if change could potentially be created).
				// There can be change only if the remaining is at least the current denom effective cost + the minimum change effective cost.
				if (denom is null ||
					remaining < denom.EffectiveCost + MinAllowedOutputAmount + ChangeFee && remainingVsize < denom.ScriptType.EstimateOutputVsize() ||
					remaining >= denom.EffectiveCost + MinAllowedOutputAmount + ChangeFee && remainingVsize < denom.ScriptType.EstimateOutputVsize() + ChangeScriptType.EstimateOutputVsize())
				{
					break;
				}

				currentSet.Add(denom);
				remaining -= denom.EffectiveCost;
				remainingVsize -= denom.ScriptType.EstimateOutputVsize();

				// Can't have more denoms than max - 1, where -1 is to account for possible change.
				if (currentSet.Count >= maxNumberOfOutputsAllowed - 1)
				{
					break;
				}
			}

			var loss = Money.Zero;
			if (remaining >= MinAllowedOutputAmount + ChangeFee)
			{
				var change = Output.FromAmount(remaining, ChangeScriptType, FeeRate);
				currentSet.Add(change);
			}
			else
			{
				// This goes to miners.
				loss = remaining;
			}

			setCandidates.TryAdd(
				CalculateHash(currentSet), // Create hash to ensure uniqueness.
				(currentSet, loss + CalculateCost(currentSet)));
		}

		return setCandidates;
	}

	private KeyValuePair<int, (IEnumerable<Output> Decomp, Money Cost)> CreateNaiveDecomposition(IEnumerable<Output> denoms, Money myInputSum, int maxNumberOfOutputsAllowed)
	{
		var remainingVsize = AvailableVsize;
		var remaining = myInputSum;
		List<Output> naiveSet = new();

		foreach (var denom in denoms.Where(x => x.Amount <= remaining))
		{
			bool end = false;
			while (denom.EffectiveCost <= remaining)
			{
				// Continue only if there is enough remaining amount and size to create one output + change (if change will potentially be created).
				// There can be change only if the remaining is at least the current denom effective cost + the minimum change effective cost.
				if (remaining < denom.EffectiveCost + MinAllowedOutputAmount + ChangeFee && remainingVsize < denom.ScriptType.EstimateOutputVsize() ||
					remaining >= denom.EffectiveCost + MinAllowedOutputAmount + ChangeFee && remainingVsize < denom.ScriptType.EstimateOutputVsize() + ChangeScriptType.EstimateOutputVsize())
				{
					end = true;
					break;
				}

				naiveSet.Add(denom);
				remaining -= denom.EffectiveCost;
				remainingVsize -= denom.ScriptType.EstimateOutputVsize();

				// Can't have more denoms than max - 1, where - 1 is to account for possible change.
				if (naiveSet.Count >= maxNumberOfOutputsAllowed - 1)
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

		var loss = Money.Zero;
		if (remaining >= MinAllowedOutputAmount + ChangeFee)
		{
			naiveSet.Add(Output.FromAmount(remaining, ChangeScriptType, FeeRate));
		}
		else
		{
			// This goes to miners.
			loss = remaining;
		}

		// This can happen when smallest denom is larger than the input sum.
		if (naiveSet.Count == 0)
		{
			naiveSet.Add(Output.FromAmount(remaining, ChangeScriptType, FeeRate));
		}

		return KeyValuePair.Create(CalculateHash(naiveSet), ((IEnumerable<Output>)naiveSet, loss + CalculateCost(naiveSet)));
	}

	/// <returns>Pair of denomination and the number of times we found it in a breakdown.</returns>
	private Dictionary<Output, long> GetDenominationFrequencies(IEnumerable<Money> inputEffectiveValues)
	{
		var secondLargestInput = inputEffectiveValues.OrderByDescending(x => x).Skip(1).First();
		var demonsForBreakDown = Denominations
			.Where(x => x.EffectiveCost <= secondLargestInput) // Take only affordable denominations.
			.OrderByDescending(x => x.EffectiveAmount); // If the amount is the same, the cheaper to spend should be the first - so greedy will take that.

		Dictionary<Output, long> denomFrequencies = new();
		foreach (var input in inputEffectiveValues)
		{
			var denominations = BreakDown(input, demonsForBreakDown);

			foreach (var denom in denominations)
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
	private IEnumerable<Output> BreakDown(Money coinInputEffectiveValue, IEnumerable<Output> denominations)
	{
		var remaining = coinInputEffectiveValue;

		List<Output> denoms = new();

		foreach (var denom in denominations)
		{
			if (denom.Amount < MinAllowedOutputAmount || remaining < MinAllowedOutputAmount + ChangeFee)
			{
				break;
			}

			while (denom.EffectiveCost <= remaining)
			{
				denoms.Add(denom);
				remaining -= denom.EffectiveCost;
			}
		}

		if (remaining >= MinAllowedOutputAmount + ChangeFee)
		{
			denoms.Add(Output.FromAmount(remaining, ChangeScriptType, FeeRate));
		}

		return denoms;
	}

	private Money CalculateCost(IEnumerable<Output> outputs)
	{
		// The cost of the outputs. The more the worst.
		var outputCost = outputs.Sum(o => o.Fee);

		// The cost of sending further or remix these coins.
		var inputCost = outputs.Sum(o => o.InputFee);

		return outputCost + inputCost;
	}

	private int CalculateHash(IEnumerable<Output> outputs)
	{
		HashCode hash = new();
		foreach (var item in outputs.OrderBy(x => x.EffectiveCost))
		{
			hash.Add(item.Amount);
		}
		return hash.ToHashCode();
	}
}
