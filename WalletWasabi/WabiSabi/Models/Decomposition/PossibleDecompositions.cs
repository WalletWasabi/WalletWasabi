using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models.Decomposition
{
	// TODO split Decomposition into two variants:
	// - an array of longs with the sum as the first element, used internally in
	//   this class
	// - a record type built around an IEnumerable<Money> with the sum computed,
	//   which is returned from the ByEffectiveCost API.
	//
	// using just a single array means that ordering lexicographically is
	// equivalent to sorting by total effective sum and then lexicographically,
	// which can remove some of the code and also allow representing each size
	// class as a rank 2 array instead of an array of structs containing arrays,
	// which is more compact.
	public class PossibleDecompositions
	{
		private PossibleDecompositions(
			IEnumerable<long> effectiveCosts,
			long maximumEffectiveCost,
			long minimumEffectiveCost,
			int maxCount)
		{
			Debug.Assert(maxCount >= 0);

			// TODO Instead of effective costs, use nominal values internally.
			// Since decompositions are segregated by size their total effective
			// values can just be shifted by the cost per output multiplied by
			// the size. This allows computations to be shared between rounds
			// even with different feerates.
			var orderedDenoms = effectiveCosts.Where(x => x <= maximumEffectiveCost).OrderByDescending(x => x).ToImmutableArray();

			MinimumEffectiveCost = minimumEffectiveCost;
			MaximumEffectiveCost = maximumEffectiveCost;

			ByCountThenEffectiveCost = new(maxCount);

			for (var i = maxCount; i > 0; i--)
			{
				var extendedCombinations = Extend(ByCountThenEffectiveCost.LastOrDefault(),
												  orderedDenoms,
												  maximumEffectiveCost,
												  i == 1 ? minimumEffectiveCost : 0);

				// Materialize to an array, so that the next iteration of the
				// loop can use this iteration's results with efficient pruning.
				// TODO memoize, but do so lazily, while still allowing Prune to
				// work on the partially materialized part.
				ByCountThenEffectiveCost.Add(extendedCombinations.ToArray());
			}
		}

		// Decompositions are kept separated by the size of the combination, and
		// then by effective cost. Segregating by size ensures that the
		// individual arrays are ordered both by total effective cost and
		// lexicographically, which is required to generate them efficiently.
		private List<Decomposition[]> ByCountThenEffectiveCost { get; }

		private long MaximumEffectiveCost;

		private long MinimumEffectiveCost;

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
			Debug.Assert(minimumEffectiveCost >= MinimumEffectiveCost || minimumEffectiveCost == long.MinValue);
			Debug.Assert(maximumEffectiveCost <= MaximumEffectiveCost || maximumEffectiveCost == long.MaxValue);
			Debug.Assert(maxOutputs <= ByCountThenEffectiveCost.Count || maxOutputs == int.MaxValue);

			return ByCountThenEffectiveCost
				.Take(maxOutputs)
				.Select(xdecompositions => Prune(xdecompositions,
								   Math.Min(maximumEffectiveCost, MaximumEffectiveCost),
								   Math.Max(minimumEffectiveCost, MinimumEffectiveCost)))
				.Aggregate(ImmutableArray<Decomposition>.Empty as IEnumerable<Decomposition>, Merge)
				.Take(maxDecompositions);
		}

		public static PossibleDecompositions Generate(
			IEnumerable<Money> nominalValues,
			long maximumEffectiveCost,
			long minimumEffectiveCost,
			int maxCount,
			FeeRate? feeRate = null,
			int vsizePerOutput = Constants.P2WPKHOutputSizeInBytes)
			=> Generate(nominalValues, maximumEffectiveCost, minimumEffectiveCost, maxCount, (feeRate ?? FeeRate.Zero).GetFee(vsizePerOutput));

		public static PossibleDecompositions Generate(
			IEnumerable<Money> nominalValues,
			long maximumEffectiveCost,
			long minimumEffectiveCost,
			int maxCount,
			Money costPerOutput)
			=> new PossibleDecompositions(nominalValues.Select(x => (x + costPerOutput).Satoshi),
										  maximumEffectiveCost,
										  minimumEffectiveCost,
										  maxCount);

		// TODO generate using multiple cores.
		// AsParallel() has no apparent effect, parallelism only seems to make
		// things unbearably slow if the inner `Extend` is also made
		// AsParallel(), otherwise it seems to be stuck in a sequential
		// execution mode. In addition to the below, also tried a
		// partitioner.
		// .AsParallel()
		// .AsUnordered()
		// .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
		// .WithMergeOptions(ParallelMergeOptions.NotBuffered)
		// .WithDegreeOfParallelism(PossibleEffectiveCosts.Length)
		private static IEnumerable<Decomposition> Extend(Decomposition[]? orderedDecompositions, IEnumerable<long> outputEffectiveCosts, long maximumEffectiveCost, long minimumEffectiveCost)
			=> outputEffectiveCosts
			.Select(x => Extend(orderedDecompositions, x, maximumEffectiveCost, minimumEffectiveCost))
			.Aggregate(ImmutableArray<Decomposition>.Empty as IEnumerable<Decomposition>, Merge);

		// Extend a set of decompositions with a specific output value,
		// producing combinations that sum to between MinEffectiveCost and
		// MaxEffectiveCost.
		private static IEnumerable<Decomposition> Extend(Decomposition[]? orderedDecompositions, long outputEffectiveCost, long maximumEffectiveCost, long minimumEffectiveCost)
			=> orderedDecompositions switch {
			// When there is nothing to combine with, generate singletons.
			null => ImmutableArray.Create<Decomposition>(new Decomposition(ImmutableArray.Create<long>(outputEffectiveCost))),

			// Otherwise, extend the relevant partial decompositions with the
			// specified value.
			//
			// The decompositions in the array are of a uniform size, and
			// ordered both lexicgraphically (the values of the individual
			// outputs) and by total effective value, both descending.
			//
			// First we prune by total effective cost of each decomposition,
			// basedf on the target range.
			//
			// Then we prune lexicographically, ignoring decompositions that
			// begin with a higher value.
			//
			// Finally we filter, leaving only decompositions which terminate in
			// a larger value. We can't prune because the last This ensures that
			// all decompositions are unique.
			_ => PruneLexicographically(
				Prune(orderedDecompositions, maximumEffectiveCost - outputEffectiveCost, minimumEffectiveCost - outputEffectiveCost), // FindIndex can handle negatives
				outputEffectiveCost)
			.Where(x => outputEffectiveCost <= x.Outputs.Last())
			.Select(x => x.Extend(outputEffectiveCost))
		};

		// Prune an array of decompositions, restricting to a range of total
		// effective values.
		internal static Decomposition[] Prune(Decomposition[]? orderedDecompositions, long maximumEffectiveCost, long minimumEffectiveCost)
			=> orderedDecompositions switch
		{
			null => new Decomposition[]{},
			_ => orderedDecompositions[new Range(FindIndex(orderedDecompositions, maximumEffectiveCost),
												 FindIndex(orderedDecompositions, minimumEffectiveCost - 1))],
		};

		// Prune an array of decompositions, ensuring that the largest output in
		// the remaining range is greater than LargestOutput.
		internal static Decomposition[] PruneLexicographically(Decomposition[] orderedDecompositions, long largetstOutputLowerBound)
		=> orderedDecompositions[Range.EndAt(FindIndex(orderedDecompositions,
													   new Decomposition(ImmutableArray.Create<long>(largetstOutputLowerBound - 1)),
													   new LexicographicalComparer()))];

		// Find an index for a given total effective value, or the insert where
		// it would be inserted.
		private static Index FindIndex(Decomposition[] orderedDecompositions, long targetEffectiveCost)
		{
			if (orderedDecompositions.Length == 0)
			{
				return new Index(0);
			}
			else if (targetEffectiveCost >= orderedDecompositions[0].EffectiveCost)
			{
				return new Index(0);
			}
			else if (targetEffectiveCost < orderedDecompositions[^1].EffectiveCost)
			{
				return new Index(0, true);
			}
			else
			{
				return FindIndex(orderedDecompositions, new Decomposition(targetEffectiveCost), new EffectiveCostComparer());
			}
		}

		// Find an index by binary searching using a comparer.
		private static Index FindIndex(Decomposition[] orderedDecompositions, Decomposition prototype, IComparer<Decomposition> comparer)
		{
			var i = Array.BinarySearch<Decomposition>(orderedDecompositions, prototype, comparer);

			if (i < 0)
			{
				return new Index(~i);
			}
			else
			{
				// BinarySearch doesn't necessarily return the first entry, so
				// do an additional backwards linear scan to find it.
				while (i > 0 && comparer.Compare(orderedDecompositions[i-1], orderedDecompositions[i]) == 0)
				{
					i--;
				}

				return new Index(i);
			}
		}

		// Merge two ordered enumerables (could be generic in T where T : IComparable)
		private static IEnumerable<Decomposition> Merge(IEnumerable<Decomposition> a, IEnumerable<Decomposition> b)
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

		private class EffectiveCostComparer : Comparer<Decomposition>
		{
			// Compare only using the effective cost, ignoring the individual
			// outputs, used in FindIndex. Note that the order is always descending,
			// so the arguments are reversed.
			public override int Compare(Decomposition x, Decomposition y)
				=> y.EffectiveCost.CompareTo(x.EffectiveCost);
		}

		private class LexicographicalComparer : Comparer<Decomposition>
		{
			// Compare only using the first (largest) output value.
			public override int Compare(Decomposition x, Decomposition y)
				=> y.Outputs[0].CompareTo(x.Outputs[0]);
		}
	}
}
