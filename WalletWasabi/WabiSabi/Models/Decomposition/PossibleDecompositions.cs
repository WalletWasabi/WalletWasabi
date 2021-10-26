using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models.Decomposition
{
	public class PossibleDecompositions
	{
		private PossibleDecompositions(
			IEnumerable<long> effectiveCosts,
			long maximumEffectiveCost,
			long minimumEffectiveCost,
			int maxOutputs)
		{
			Debug.Assert(maxOutputs > 0);

			// TODO Instead of effective costs, use nominal values internally.
			// Since decompositions are segregated by size their total effective
			// values can just be shifted by the cost per output multiplied by
			// the size. This allows computations to be shared between rounds
			// even with different feerates.
			var orderedDenoms = effectiveCosts.Where(x => x <= maximumEffectiveCost).OrderByDescending(x => x).ToImmutableArray();

			MaximumEffectiveCost = maximumEffectiveCost;
			MinimumEffectiveCost = minimumEffectiveCost;

			// TODO support different effective cost prune ranges for different
			// sized combinations, using a higher max value and 0 minimum for up
			// to size 4 to query likely denoms, but only the wallet's
			// registered balance and tightly bounded objective loss for
			// optimizing decomposition, to improve efficiency.
			if (maxOutputs == 1)
			{
				var prunedSingletons = new DecompositionsOfASize(orderedDenoms, maximumEffectiveCost, minimumEffectiveCost);
				StratifiedDecompositions = ImmutableArray.Create<DecompositionsOfASize>(prunedSingletons);
			}
			else
			{
				var bySize = ImmutableArray.CreateBuilder<DecompositionsOfASize>(maxOutputs);

				// Generate the base decompositions, one for each possible value,
				// without pruning by minimum effective cost.
				var unprunedSingletons = new DecompositionsOfASize(orderedDenoms, maximumEffectiveCost, 0);
				bySize.Add(unprunedSingletons);

				// Extend to create combinations smaller than maxOutputs.
				// There is still no pruning by minimum value as these
				// decompositions are not yet complete.
				while (bySize.Capacity - bySize.Count > 1)
				{
					bySize.Add(bySize.Last().Extend(maximumEffectiveCost, 0));
				}

				// The final extension can make use of the minimum value bound.
				bySize.Add(bySize.Last().Extend(maximumEffectiveCost, minimumEffectiveCost));

				StratifiedDecompositions = bySize.MoveToImmutable();
			}
		}

		// Decompositions are kept separated by the size of the combination, and
		// then by effective cost. Stratifying by size ensures that the
		// individual arrays are ordered both by total effective cost and
		// lexicographically, which is required to generate them efficiently.
		private ImmutableArray<DecompositionsOfASize> StratifiedDecompositions { get; }

		private long MaximumEffectiveCost;

		private long MinimumEffectiveCost;

		private long MaxOutputs => StratifiedDecompositions.Length;

		public static PossibleDecompositions Generate(
			IEnumerable<Money> nominalValues,
			long maximumEffectiveCost,
			long minimumEffectiveCost,
			int maxOutputs,
			FeeRate? feeRate = null,
			int vsizePerOutput = Constants.P2WPKHOutputSizeInBytes)
			=> Generate(nominalValues, maximumEffectiveCost, minimumEffectiveCost, maxOutputs, (feeRate ?? FeeRate.Zero).GetFee(vsizePerOutput));

		public static PossibleDecompositions Generate(
			IEnumerable<Money> nominalValues,
			long maximumEffectiveCost,
			long minimumEffectiveCost,
			int maxOutputs,
			Money costPerOutput)
			=> new PossibleDecompositions(nominalValues.Select(x => (x + costPerOutput).Satoshi),
										  maximumEffectiveCost,
										  minimumEffectiveCost,
										  maxOutputs);

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
		public IEnumerable<Decomposition> ByEffectiveCost(long maximumEffectiveCost = long.MaxValue,
														  long minimumEffectiveCost = long.MinValue,
														  int maxOutputs = int.MaxValue,
														  int maxDecompositions = 1000)
		{
			// FIXME better way to handle this? the values need to be in range,
			// but they should be optional. overloads? nullable?
			Debug.Assert(maximumEffectiveCost <= MaximumEffectiveCost || maximumEffectiveCost == long.MaxValue);
			Debug.Assert(minimumEffectiveCost >= MinimumEffectiveCost || minimumEffectiveCost == long.MinValue);
			Debug.Assert(maxOutputs <= StratifiedDecompositions.Length || maxOutputs == int.MaxValue);

			return StratifiedDecompositions
				.Take(maxOutputs)
				.Select(decompositions => decompositions.Prune(Math.Min(maximumEffectiveCost, MaximumEffectiveCost),
															   Math.Max(minimumEffectiveCost, MinimumEffectiveCost)))
				.Aggregate(ImmutableArray<Decomposition>.Empty as IEnumerable<Decomposition>, Merge)
				.Take(maxDecompositions);
		}

		// Merge two ordered enumerables (could be generic in T where T : IComparable)
		internal static IEnumerable<Decomposition> Merge(IEnumerable<Decomposition> a, IEnumerable<Decomposition> b)
		{
			if (!a.Any())
			{
				return b;
			}

			if (!b.Any())
			{
				return a;
			}

			IEnumerable<Decomposition> generator()
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
					if (cmp > 0)
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

			return generator();
		}
	}
}
