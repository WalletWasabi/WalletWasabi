using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;

namespace WalletWasabi.WabiSabi.Models.Decomposition
{
	// Represents an ordered set of possible decompositions (multisets of output
	// values) with a specific number of outputs, and support for efficient
	// range queries based on some constraints used to efficiently allow
	// extending to the next size up.
	//
	// TODO
	// Internally a more compact representation two arrays of longs or an array
	// of a pair of longs can be used, with one for the sum and the other
	// encoding one index from the denomination set per 8 bits, for combinations
	// of size 8, with two longs supporting combinations up to size 16.
	//
	// Unlike a long[,] representation this would still amenable to linq and use
	// with ArrayBuilder and ReadOnlySpan, and can be converted lazily after
	// pruning/querying to a `Decomposition` object (with fully reified Money
	// properties for the nominal and effective values) lazily for a higher
	// level interface that still doesn't waste GBs of memory or cause
	// significant GC load like the current approach (which is effectively a
	// jagged array).
	//
	// Computing ombinations up to size 5 without minimum value pruning up to
	// around 0.02 BTC (1e6 + 2^20 sats) takes roughly 1GB of RAM (and about 30
	// seconds on an i7) which is reasonable as a precomputation, but larger
	// balances will need more storage as well as larger decompositions, so a
	// more compact representation is desirable.
	//
	// Since the sizes are the same, they can be shifted at query time allowing
	// for nominal values to be used instead of effective value which means that
	// the combinations could be pre-computed once and saved to disk in a more
	// compact representation for reuse between rounds even when considering
	// large combinations and many random queries over a large effective value
	// range (e.g. for coin selection).
	internal record DecompositionsOfASize
	{
		public DecompositionsOfASize(IEnumerable<long> denominations, long max, long min)
			: this(denominations.OrderByDescending(x => x).ToImmutableArray(), max, min)
		{
		}

		private DecompositionsOfASize(ImmutableArray<long> denominations, long maximumEffectiveCost, long minimumEffectiveCost)
		{
			Denominations = denominations;

			ByEffectiveCost = Denominations
				.Select(x => new Decomposition(x))
				.ToImmutableArray();

			MaximumEffectiveCost = maximumEffectiveCost;

			MinimumEffectiveCost = minimumEffectiveCost;
		}

		public ImmutableArray<Decomposition> ByEffectiveCost { get; private init; }

		public ImmutableArray<long> Denominations { get; }

		public int Count => ByEffectiveCost.Length;

		public int Size => ByEffectiveCost[0].Outputs.Length;

		public long MaximumEffectiveCost { get; private init; }

		public long MinimumEffectiveCost { get; private init; }

		// Returns the set of decompositions of the next size up by extending
		// the decompositions of the current set with each of the base
		// denominations.
		//
		// This is done by generating one set for each base value, which retains
		// the order, and merging the resulting sets to preserve the global
		// ordering.
		internal DecompositionsOfASize Extend(long maximumEffectiveCost, long minimumEffectiveCost)
			=> this with
			{
				MaximumEffectiveCost = Math.Min(MaximumEffectiveCost, maximumEffectiveCost),
				MinimumEffectiveCost = Math.Max(MinimumEffectiveCost, minimumEffectiveCost),

				ByEffectiveCost = Denominations
				// FIXME
				// AsParallel() has no apparent effect, parallelism only seems to make
				// things unbearably slow if the inner `Extend` is also made
				// AsParallel(), otherwise it seems to be stuck in a sequential
				// execution mode. In addition to the below, also tried a
				// partitioner.
				// .AsParallel()
				// .AsUnordered()
				// .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
				// .WithMergeOptions(ParallelMergeOptions.NotBuffered)
				// .WithDegreeOfParallelism(Denominations.Length)
				.Select(x => Extend(x, maximumEffectiveCost, minimumEffectiveCost))
				.Aggregate(PossibleDecompositions.Merge)
				.ToImmutableArray(),
			};

		// Extend a set of decompositions with a specific output value,
		// producing combinations that sum to between MinEffectiveCost and
		// MaxEffectiveCost.
		//
		// The decompositions in the array are of a uniform size, and ordered
		// both lexicgraphically (the values of the individual outputs) and by
		// total effective value, both descending. Since these values are
		// extended using provided value, the ordering is preserved.
		//
		// First we prune by total effective cost of each decomposition,
		// based on the target range.
		//
		// Then we prune lexicographically, ignoring decompositions that
		// begin with a higher value.
		//
		// Finally we filter, leaving only decompositions which terminate in
		// a larger value. We can't prune because the last This ensures that
		// all decompositions are unique.
		private IEnumerable<Decomposition> Extend(long additionalOutput, long maximumEffectiveCost, long minimumEffectiveCost)
			=> Prune(maximumEffectiveCost - additionalOutput, minimumEffectiveCost - additionalOutput, additionalOutput) // FindIndex can handle negative values
			.Where(x => additionalOutput <= x.Outputs.Last())
			.Select(x => x.Extend(additionalOutput));

		// High level pruning of the set of decompositions, both
		// lexicographically and by the total effective value.
		internal IEnumerable<Decomposition> Prune(long maximumEffectiveCost, long minimumEffectiveCost, long largestOutputLowerBound)
			=> MemoryMarshal.ToEnumerable(
				PruneLexicographically(
					PruneByEffectiveCost(ByEffectiveCost.AsMemory(), maximumEffectiveCost, minimumEffectiveCost),
					largestOutputLowerBound));

		// High level pruning of the set of decompositions, only
		// by the total effective value.
		internal IEnumerable<Decomposition> Prune(long maximumEffectiveCost, long minimumEffectiveCost)
			=> MemoryMarshal.ToEnumerable(
				PruneByEffectiveCost(ByEffectiveCost.AsMemory(), MaximumEffectiveCost, minimumEffectiveCost));

		// Prune an array of decompositions, ensuring that the largest output in
		// the remaining range is greater than LargestOutput.
		private static ReadOnlyMemory<Decomposition> PruneLexicographically(ReadOnlyMemory<Decomposition> orderedDecompositions, long largetstOutputLowerBound)
			=> orderedDecompositions[Range.EndAt(FindIndex(orderedDecompositions.Span,
														   new Decomposition(largetstOutputLowerBound - 1),
														   new LexicographicalComparer()))];

		// Prune an array of decompositions, restricting to a range of total
		// effective values.
		// This is a private method, made internal so that it has unit tests.
		internal static ReadOnlyMemory<Decomposition> PruneByEffectiveCost(ReadOnlyMemory<Decomposition> byEffectiveCost, long maximumEffectiveCost, long minimumEffectiveCost)
			=> byEffectiveCost[new Range(FindIndex(byEffectiveCost.Span, maximumEffectiveCost),
										 FindIndex(byEffectiveCost.Span, minimumEffectiveCost - 1))];

		// Find an index for a given total effective value, or the insert where
		// it would be inserted.
		private static Index FindIndex(ReadOnlySpan<Decomposition> orderedDecompositions, long targetEffectiveCost)
		{
			if (orderedDecompositions.Length == 0)
			{
				return 0;
			}
			else if (targetEffectiveCost >= orderedDecompositions[0].EffectiveCost)
			{
				return 0;
			}
			else if (targetEffectiveCost < orderedDecompositions[^1].EffectiveCost)
			{
				return ^0;
			}
			else
			{
				return FindIndex(orderedDecompositions, new Decomposition(targetEffectiveCost), new EffectiveCostComparer());
			}
		}

		// Find an index by binary searching using a comparer.
		private static Index FindIndex(ReadOnlySpan<Decomposition> orderedDecompositions, Decomposition prototype, IComparer<Decomposition> comparer)
		{
			var i = orderedDecompositions.BinarySearch(prototype, comparer);

			if (i < 0)
			{
				return ~i;
			}
			else
			{
				// BinarySearch doesn't necessarily return the first entry, so
				// do an additional backwards linear scan to find it.
				while (i > 0 && comparer.Compare(orderedDecompositions[i-1], orderedDecompositions[i]) == 0)
				{
					i--;
				}

				return i;
			}
		}

		private class EffectiveCostComparer : Comparer<Decomposition>
		{
			// Compare only using the effective cost, ignoring the individual
			// outputs, used in FindIndex. Note that the order is always descending,
			// so the arguments are reversed.
			public override int Compare(Decomposition? x, Decomposition? y)
				=> (x, y) switch
				{
					(null, null) => 0,
					(null, _) => 1,
					(_, null) => -1,
					({} right, {} left) =>	left.EffectiveCost.CompareTo(right.EffectiveCost)
				};
		}

		private class LexicographicalComparer : Comparer<Decomposition>
		{
			// Compare only using the first (largest) output value.
			public override int Compare(Decomposition x, Decomposition y)
				=> y.Outputs[0].CompareTo(x.Outputs[0]);
		}
	}
}
