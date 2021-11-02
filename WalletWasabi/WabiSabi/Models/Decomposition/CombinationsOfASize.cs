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
	internal record CombinationsOfASize
	{
		public CombinationsOfASize(ImmutableArray<long> values, long maximumTotalValue, long minimumTotalValue)
		{
			Values = values.OrderByDescending(x => x).ToImmutableArray();

			ByTotalValue = Values
				.Select(x => new Decomposition(x))
				.ToImmutableArray();

			MaximumTotalValue = maximumTotalValue;

			MinimumTotalValue = minimumTotalValue;
		}

		public ImmutableArray<Decomposition> ByTotalValue { get; private init; }

		public ImmutableArray<long> Values { get; }

		public int Count => ByTotalValue.Length;

		public int Size => ByTotalValue[0].Outputs.Length;

		public long MaximumTotalValue { get; private init; }

		public long MinimumTotalValue { get; private init; }

		// Returns the set of decompositions of the next size up by extending
		// the decompositions of the current set with each of the base
		// denominations.
		//
		// This is done by generating one set for each base value, which retains
		// the order, and merging the resulting sets to preserve the global
		// ordering.
		internal CombinationsOfASize Extend(long maximumTotalValue, long minimumTotalValue)
			=> this with
			{
				MaximumTotalValue = Math.Min(MaximumTotalValue, maximumTotalValue),
				MinimumTotalValue = Math.Max(MinimumTotalValue, minimumTotalValue),

				ByTotalValue = Values
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
				.Select(x => Extend(x, maximumTotalValue, minimumTotalValue))
				.Aggregate(PossibleDecompositions.MergeDescending)
				.ToImmutableArray(),
			};

		// Extend a set of decompositions with a specific output value,
		// producing combinations that sum to between MinimumTotalValue and
		// MaximumTotalValue.
		//
		// The decompositions in the array are of a uniform size, and ordered
		// both lexicgraphically (the values of the individual outputs) and by
		// total value, both descending. Since these values are extended using
		// additional value, the ordering is preserved.
		//
		// First we prune by total value of each decomposition, based on the
		// target range.
		//
		// Then we prune lexicographically, ignoring decompositions that
		// begin with a higher value.
		//
		// Finally we filter, leaving only decompositions which terminate in
		// a larger value. We can't prune because the last This ensures that
		// all decompositions are unique.
		private IEnumerable<Decomposition> Extend(long additionalOutput, long maximumTotalValue, long minimumTotalValue)
			=> Prune(maximumTotalValue - additionalOutput, minimumTotalValue - additionalOutput, additionalOutput) // FindIndex can handle negative values
			.Where(x => additionalOutput <= x.Outputs.Last())
			.Select(x => x.Extend(additionalOutput));

		// High level pruning of the set of decompositions, both
		// lexicographically and by the total value.
		internal IEnumerable<Decomposition> Prune(long maximumTotalValue, long minimumTotalValue, long largestOutputLowerBound)
			=> MemoryMarshal.ToEnumerable(
				PruneLexicographically(
					PruneByTotalValue(ByTotalValue.AsMemory(), maximumTotalValue, minimumTotalValue),
					largestOutputLowerBound));

		// Prune an array of decompositions, ensuring that the largest output in
		// the remaining range is greater than LargestOutput.
		private static ReadOnlyMemory<Decomposition> PruneLexicographically(ReadOnlyMemory<Decomposition> orderedDecompositions, long largetstOutputLowerBound)
			=> orderedDecompositions[Range.EndAt(FindIndex(orderedDecompositions.Span,
														   new Decomposition(largetstOutputLowerBound - 1),
														   new ReverseLexicographicalComparer()))];

		// Prune an array of decompositions, restricting to a range of total
		// values.
		// This is a private method, made internal so that it has unit tests.
		internal static ReadOnlyMemory<Decomposition> PruneByTotalValue(ReadOnlyMemory<Decomposition> byTotalValue, long maximum, long minimum)
			=> byTotalValue[new Range(FindIndex(byTotalValue.Span, maximum),
										 FindIndex(byTotalValue.Span, minimum - 1))];

		// Find an index for a given total value, or the insert where
		// it would be inserted.
		private static Index FindIndex(ReadOnlySpan<Decomposition> orderedDecompositions, long target)
		{
			if (orderedDecompositions.Length == 0)
			{
				return 0;
			}
			else if (target >= orderedDecompositions[0].TotalValue)
			{
				return 0;
			}
			else if (target < orderedDecompositions[^1].TotalValue)
			{
				return ^0;
			}
			else
			{
				return FindIndex(orderedDecompositions, new Decomposition(target), new ReverseTotalValueComparer());
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

		private class ReverseTotalValueComparer : Comparer<Decomposition>
		{
			// Compare only using the total value, ignoring the individual
			// outputs, used in FindIndex. Note that the order is always
			// descending, so the arguments are reversed.
			public override int Compare(Decomposition? x, Decomposition? y)
				=> (x, y) switch
				{
					(null, null) => 0,
					(null, _) => 1,
					(_, null) => -1,
					({} right, {} left) =>	left.TotalValue.CompareTo(right.TotalValue)
				};
		}

		private class ReverseLexicographicalComparer : Comparer<Decomposition>
		{
			// Compare only using the first (largest) output value.
			public override int Compare(Decomposition? x, Decomposition? y)
				=> (x, y) switch
				{
					(null, null) => 0,
					(null, _) => 1,
					(_, null) => -1,
					({} right, {} left)=> y.Outputs[0].CompareTo(x.Outputs[0])
				};
		}
	}
}
