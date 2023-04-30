using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client;

/// <summary>
/// Pull requests to this file must be up to date with this simulation to ensure correctness: https://github.com/nopara73/Sake
/// </summary>
public class AmountDecomposer
{
	/// <param name="feeRate">Bitcoin network fee rate the coinjoin is targeting.</param>
	/// <param name="allowedOutputAmount">Range of output amount that's allowed to be registered.</param>
	/// <param name="availableVsize">Available virtual size for outputs.</param>
	/// <param name="random">Allows testing by setting a seed value for the random number generator. Use <c>null</c> in production code.</param>
	public AmountDecomposer(FeeRate feeRate, MoneyRange allowedOutputAmount, int availableVsize, bool isTaprootAllowed, WasabiRandom random, bool isSegwitAllowed = true)
	{
		FeeRate = feeRate;

		AvailableVsize = availableVsize;
		IsTaprootAllowed = isTaprootAllowed;
		IsSegwitAllowed = isSegwitAllowed;
		MinAllowedOutputAmount = allowedOutputAmount.Min;
		MaxAllowedOutputAmount = allowedOutputAmount.Max;

		Random = random;

		// Create many standard denominations.
		Denominations = CreateDenominations();

		ChangeScriptType = GetNextScriptType();
	}

	public FeeRate FeeRate { get; }
	public int AvailableVsize { get; }
	public bool IsTaprootAllowed { get; }
	public bool IsSegwitAllowed { get; }
	public Money MinAllowedOutputAmount { get; }
	public Money MaxAllowedOutputAmount { get; }

	public IOrderedEnumerable<Output> Denominations { get; }
	public ScriptType ChangeScriptType { get; }
	public Money ChangeFee => FeeRate.GetFee(ChangeScriptType.EstimateOutputVsize());
	private WasabiRandom Random { get; }

	private ScriptType GetNextScriptType()
	{
		if (!IsTaprootAllowed && !IsSegwitAllowed)
		{
			throw new Exception("One of taproot or segwit must be allowed.");
		}

		if (IsTaprootAllowed && !IsSegwitAllowed)
		{
			return ScriptType.Taproot;
		}

		if (!IsTaprootAllowed && IsSegwitAllowed)
		{
			return ScriptType.P2WPKH;
		}

		return Random.GetInt(0, 2) == 0 ? ScriptType.P2WPKH : ScriptType.Taproot;
	}

	private IOrderedEnumerable<Output> CreateDenominations()
	{
		var denominations = new HashSet<Output>();

		Output CreateDenom(double sats)
		{
			var scriptType = GetNextScriptType();
			return Output.FromDenomination(Money.Satoshis((ulong)sats), scriptType, FeeRate);
		}

		// Powers of 2
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(2, i));

			if (denom.Amount < MinAllowedOutputAmount)
			{
				continue;
			}

			if (denom.Amount > MaxAllowedOutputAmount)
			{
				break;
			}

			denominations.Add(denom);
		}

		// Powers of 3
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(3, i));

			if (denom.Amount < MinAllowedOutputAmount)
			{
				continue;
			}

			if (denom.Amount > MaxAllowedOutputAmount)
			{
				break;
			}

			denominations.Add(denom);
		}

		// Powers of 3 * 2
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(3, i) * 2);

			if (denom.Amount < MinAllowedOutputAmount)
			{
				continue;
			}

			if (denom.Amount > MaxAllowedOutputAmount)
			{
				break;
			}

			denominations.Add(denom);
		}

		// Powers of 10 (1-2-5 series)
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(10, i));

			if (denom.Amount < MinAllowedOutputAmount)
			{
				continue;
			}

			if (denom.Amount > MaxAllowedOutputAmount)
			{
				break;
			}

			denominations.Add(denom);
		}

		// Powers of 10 * 2 (1-2-5 series)
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(10, i) * 2);

			if (denom.Amount < MinAllowedOutputAmount)
			{
				continue;
			}

			if (denom.Amount > MaxAllowedOutputAmount)
			{
				break;
			}

			denominations.Add(denom);
		}

		// Powers of 10 * 5 (1-2-5 series)
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(10, i) * 5);

			if (denom.Amount < MinAllowedOutputAmount)
			{
				continue;
			}

			if (denom.Amount > MaxAllowedOutputAmount)
			{
				break;
			}

			denominations.Add(denom);
		}

		// Greedy decomposer will take the higher values first. Order in a way to prioritize cheaper denominations, this only matters in case of equality.
		return denominations.OrderByDescending(x => x.EffectiveAmount);
	}

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
			if (!denoms.Any() || denom.Amount.Satoshi <= (long)(denoms.Last().Amount.Satoshi / filterSeverity))
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
		var remaining = myInputSum;
		var remainingVsize = AvailableVsize;
		var smallestScriptType = Math.Min(ScriptType.P2WPKH.EstimateOutputVsize(), ScriptType.Taproot.EstimateOutputVsize());
		var maxNumberOfOutputsAllowed = Math.Min(AvailableVsize / smallestScriptType, 8); // The absolute max possible with the smallest script type.

		var setCandidates = new Dictionary<int, (IEnumerable<Output> Decomposition, Money Cost)>();

		// Create the most naive decomposition for starter.
		List<Output> naiveSet = new();
		bool end = false;
		foreach (var denom in denoms.Where(x => x.Amount <= remaining))
		{
			while (denom.EffectiveCost <= remaining)
			{
				// We can only let this go forward if at least 2 output can be added (denom + potential change)
				if (remaining < MinAllowedOutputAmount + ChangeFee || remainingVsize < denom.ScriptType.EstimateOutputVsize() + ChangeScriptType.EstimateOutputVsize())
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

		setCandidates.Add(
			CalculateHash(naiveSet), // Create hash to ensure uniqueness.
			(naiveSet, loss + CalculateCost(naiveSet)));

		// Create many decompositions for optimization.
		var stdDenoms = denoms.Select(d => d.EffectiveCost.Satoshi).Where(x => x <= myInputSum.Satoshi).ToArray();
		var tolerance = (long)Math.Max(loss.Satoshi, 0.5 * (ulong)(MinAllowedOutputAmount + FeeRate.GetFee(ScriptType.Taproot.EstimateOutputVsize())).Satoshi); // Assume script type with higher cost to be more permissive.

		if (maxNumberOfOutputsAllowed > 1)
		{
			foreach (var (sum, count, decomp) in Decomposer.Decompose(
				target: (long)myInputSum,
				tolerance: tolerance,
				maxCount: maxNumberOfOutputsAllowed,
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

				var deficit = (myInputSum - (ulong)finalDenoms.Sum(d => d.EffectiveCost)) + CalculateCost(finalDenoms);

				setCandidates.TryAdd(CalculateHash(finalDenoms), (finalDenoms, deficit));
			}
		}

		var denomHashSet = denoms.ToHashSet();
		var preCandidates = setCandidates.Select(x => x.Value).ToList();
		preCandidates.Shuffle(Random);

		var orderedCandidates = preCandidates
			.OrderBy(x => x.Decomposition.Sum(y => denomHashSet.Contains(y) ? Money.Zero : y.Amount)) // Prefer lower change.
			.ThenBy(x => x.Cost) // Less cost is better.
			.ThenBy(x => x.Decomposition.Any(d => d.ScriptType == ScriptType.Taproot) && x.Decomposition.Any(d => d.ScriptType == ScriptType.P2WPKH) ? 0 : 1) // Prefer mixed scripts types.
			.Select(x => x).ToList();

		// We want to introduce randomness between the best selections.
		var bestCandidateCost = orderedCandidates.First().Cost;
		var costTolerance = Money.Coins(bestCandidateCost.ToUnit(MoneyUnit.BTC) * 1.2m);
		var finalCandidates = orderedCandidates.Where(x => x.Cost <= costTolerance).ToArray();

		// We want to make sure our random selection is not between similar decompositions.
		// Different largest elements result in very different decompositions.
		var largestAmount = finalCandidates.Select(x => x.Decomposition.First()).ToHashSet().RandomElement(Random);
		var finalCandidate = finalCandidates.Where(x => x.Decomposition.First() == largestAmount).RandomElement(Random).Decomposition;

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
