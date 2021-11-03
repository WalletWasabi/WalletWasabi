using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models.Decomposition
{
	public class PossibleDecompositions
	{
		public PossibleDecompositions(
			IEnumerable<Money> nominalValues,
			Money maximumTotalValue,
			Money minimumTotalValue,
			int maxOutputs)
		{
			// 8 is more than enough to be unreasonable in practically any
			// situation.
			Guard.InRangeAndNotNull(nameof(maxOutputs), maxOutputs, 1, 8);

			var orderedDenoms = nominalValues.Where(x => x <= maximumTotalValue).OrderByDescending(x => x).ToImmutableArray();

			MaximumTotalValue = maximumTotalValue;
			MinimumTotalValue = minimumTotalValue;

			// TODO support different effective cost prune ranges for different
			// sized combinations, using a higher max value and 0 minimum for up
			// to size 4 to query likely denoms, but only the wallet's
			// registered balance and tightly bounded objective loss for
			// optimizing decomposition, to improve efficiency.
			if (maxOutputs == 1)
			{
				var prunedSingletons = new CombinationsOfASize(orderedDenoms, maximumTotalValue, minimumTotalValue);
				StratifiedDecompositions = ImmutableArray.Create<CombinationsOfASize>(prunedSingletons);
			}
			else
			{
				var bySize = ImmutableArray.CreateBuilder<CombinationsOfASize>(maxOutputs);

				// Generate the base decompositions, one for each possible value,
				// without pruning by minimum total value.
				var unprunedSingletons = new CombinationsOfASize(orderedDenoms, maximumTotalValue, 0);
				bySize.Add(unprunedSingletons);

				// Extend to create combinations smaller than maxOutputs.
				// There is still no pruning by minimum value as these
				// decompositions are not yet complete.
				while (bySize.Capacity - bySize.Count > 1)
				{
					bySize.Add(bySize[^1].Extend(maximumTotalValue, 0));
				}

				// The final extension can make use of the minimum value bound.
				bySize.Add(bySize[^1].Extend(maximumTotalValue, minimumTotalValue));

				StratifiedDecompositions = bySize.MoveToImmutable();
			}
		}

		// Decompositions are kept separated by the size of the combination.
		// Stratifying by size ensures that we can simultaneously keep them
		// ordered by total value.
		private ImmutableArray<CombinationsOfASize> StratifiedDecompositions { get; }

		private Money MaximumTotalValue { get; }

		private Money MinimumTotalValue { get; }

		private int MaxOutputs => StratifiedDecompositions.Length;

		// The final public API should only allow 2, later 3 access patterns
		// efficiently:
		// - When optimizing the decomposition of a specific balance, enumerate
		//   from a specific effective cost downwards up to a limit.
		//   Decompositions are then re-ordered after evaluating the cost
		//   function. Below 4.0 btc maxSize can be 5 with negligible losses.
		//   Above this amount 6 or more outputs may be required, and a
		//   non-standard value can be forced, but generally such input balances
		//   should just be avoided.
		// - When evaluating likely denominations given others prevouts and
		//   ownership proofs, we start enumerate downwards from multiple effective
		//   values, one for each input or sum/difference of a pair of inputs,
		//   and tally the denominations used by the closest decompositions.
		//   Max effective cost = 2* largest input, can use round parameters to
		//   precompute before learning prevout amounts. Max count can be hard
		//   coded to 4 to ensure all clients can compute the same
		//   decompositions. Likelyhood should take loss into account.
		// - When evaluating input combinations, we need to query potentially
		//   very large number of different balances, looking for small
		//   decompositions with minimal losses (tight bounds). maxcount = 4? 5?
		public IEnumerable<Decomposition> GetByTotalValue(
			Money? maximumEffectiveCost = null,
			Money? minimumTotalValue = null,
			Money? minimumValue = null, // dust threshold
			int maxOutputs = int.MaxValue,
			int maxDecompositions = 1000,
			FeeRate? feeRate = null,
			int vsizePerOutput = Constants.P2WPKHOutputSizeInBytes)
		{
			Money costPerOutput = (feeRate ?? FeeRate.Zero).GetFee(vsizePerOutput).Satoshi;
			var maxDecompositionCost = StratifiedDecompositions.Length * costPerOutput;

			Guard.True(nameof(maxOutputs), maxOutputs <= StratifiedDecompositions.Length || maxOutputs == int.MaxValue, "must not exceed limit from precomputation.");

			if (maximumEffectiveCost is null)
			{
				maximumEffectiveCost = new Money(long.MaxValue);
			}
			else
			{
				Guard.True(nameof(maximumEffectiveCost), maximumEffectiveCost - maxDecompositionCost <= MaximumTotalValue, "must not exceed limit from precomputation.");
			}

			if (minimumTotalValue is null)
			{
				minimumTotalValue = Money.Zero;
			}
			else
			{
				Guard.True(nameof(minimumTotalValue), MinimumTotalValue <= minimumTotalValue && minimumTotalValue <= MaximumTotalValue, "must not be lower than limit from precomputation.");
			}

			if (minimumValue is null)
			{
				minimumValue = Money.Zero;
			}

			return StratifiedDecompositions
				.Take(maxOutputs).Select((x, i) => (Decompositions: x, TotalCost: (i + 1) * costPerOutput))
				.Select(p => p.Decompositions.Prune(
							Money.Min(maximumEffectiveCost - p.TotalCost, MaximumTotalValue),
							Money.Max(minimumTotalValue, MinimumTotalValue),
							minimumValue)
						.Where(d => d.Outputs[^1] >= minimumValue))
				.Aggregate(ImmutableArray<Decomposition>.Empty as IEnumerable<Decomposition>, MergeDescending)
				.Take(maxDecompositions);
		}

		// Merge two ordered enumerables (could be generic in T where T : IComparable)
		internal static IEnumerable<Decomposition> MergeDescending(IEnumerable<Decomposition> a, IEnumerable<Decomposition> b)
		{
			if (!a.Any())
			{
				return b;
			}

			if (!b.Any())
			{
				return a;
			}

			IEnumerable<Decomposition> Generator()
			{
				var (large, small) = (a.GetEnumerator(), b.GetEnumerator());

				// Both enumerators must be both non-empty, so the first calls to
				// MoveNext() can be unchecked.
				large.MoveNext();
				small.MoveNext();

				for (;;)
				{
					var cmp = large.Current.CompareTo(small.Current);

					// When order is reversed, swap the two enumerators, so that we
					// always output from the 'large' enumerator
					if (cmp < 0)
					{
						(large, small) = (small, large);
					}

					// Only output distinct decompositions.
					if (cmp != 0)
					{
						yield return large.Current;
					}

					if (!large.MoveNext())
					{
						do
						{
							yield return small.Current;
						}
						while (small.MoveNext());
						yield break;
					}
				}
			}

			return Generator();
		}
	}
}
