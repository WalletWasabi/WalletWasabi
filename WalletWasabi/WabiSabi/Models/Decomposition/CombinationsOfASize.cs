using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Models.Decomposition
{
	// Represents an ordered set of possible decompositions (multisets of output
	// values) with a specific number of outputs, and support for efficient
	// range queries based on some constraints used to efficiently allow
	// extending to the next size up.
	internal record CombinationsOfASize
	{
		public CombinationsOfASize(ImmutableArray<Money> values, Money maximumTotalValue, Money minimumTotalValue)
		{
			Values = values.OrderByDescending(x => x).ToImmutableArray();

			ByTotalValue = Values
				.Select(x => new Decomposition(x))
				.ToImmutableArray();

			MaximumTotalValue = maximumTotalValue;

			MinimumTotalValue = minimumTotalValue;
		}

		public ImmutableArray<Decomposition> ByTotalValue { get; private init; }

		public ImmutableArray<Money> Values { get; }

		public int Count => ByTotalValue.Length;

		public int Size => ByTotalValue[0].Outputs.Length;

		public Money MaximumTotalValue { get; private init; }

		public Money MinimumTotalValue { get; private init; }

		// Returns the set of decompositions of the next size up by extending
		// the decompositions of the current set with each of the base
		// denominations.
		//
		// This is done by generating one set for each base value, which retains
		// the order, and merging the resulting sets to preserve the global
		// ordering.
		internal CombinationsOfASize Extend(Money maximumTotalValue, Money minimumTotalValue)
			=> this with
			{
				MaximumTotalValue = Math.Min(MaximumTotalValue, maximumTotalValue),
				MinimumTotalValue = Math.Max(MinimumTotalValue, minimumTotalValue),

				ByTotalValue = Values
				.Select(x => Extend(x, maximumTotalValue, minimumTotalValue))
				.Aggregate(PossibleDecompositions.MergeDescending)
				.ToImmutableArray(),
			};

		// Extend a set of decompositions with a specific output value,
		// producing combinations that sum to between MinimumTotalValue and
		// MaximumTotalValue.
		private IEnumerable<Decomposition> Extend(Money additionalOutput, Money maximumTotalValue, Money minimumTotalValue)
		{
			// The decompositions in the array are of a uniform size, and ordered
			// both lexicgraphically (the values of the individual outputs) and by
			// total value, both descending. Since these values are extended using the same value,
			// order is preserved.
			//
			// First we prune by total value of each decomposition, based on the
			// target range.
			//
			// Then we prune lexicographically, ignoring decompositions that
			// begin with a higher value.
			//
			// FindIndex can handle negative values, so we can just subtract the
			// value by which we want to extend the selected decompositions.
			var combinationsInRange = Prune(maximumTotalValue - additionalOutput, minimumTotalValue - additionalOutput, additionalOutput);

			// Lexicographical pruning does not guarantee that the last element
			// will respect the order invariant used to avoid generating
			// non-distinct combinations, it only excludes combinations that
			// could not possibly maintain the invariant based on their first
			// element, so we need to filter here as well.
			return combinationsInRange
				.Where(x => additionalOutput <= x.Outputs.Last())
				.Select(x => x.Extend(additionalOutput));
		}

		// High level pruning of the set of decompositions, both
		// lexicographically and by the total value.
		internal IEnumerable<Decomposition> Prune(Money maximumTotalValue, Money minimumTotalValue, Money largestOutputLowerBound)
		{
			// To avoid copying these potentially large arrays, we work with
			// the array of decompositions as ReadOnlyMemory, since
			// ImmutableArrays do not yet support slicing by Range objects, but
			// ReadOnlyMemory and ReadOnlySpan do.
			var combinations = ByTotalValue.AsMemory();

			// Restrict the ordered combinations to only those within the
			// specified range of sums.
			var combinationsInRange = PruneByTotalValue(combinations, maximumTotalValue, minimumTotalValue);

			// Next, remove combinations whose largest output is smaller than
			// the supplied bound. If the largest output is smaller, then the
			// smallest output must also be smaller than the value, so this
			// reduces the work of filtering by the last value.
			combinationsInRange = PruneLexicographically(combinationsInRange, largestOutputLowerBound);

			// Finally, after selecting out the span of interest, we need to
			// convert to an IEnumerable since ReadOnlyMemory/Span are stack
			// only.
			return MemoryMarshal.ToEnumerable(combinationsInRange);
		}

		// Prune an array of decompositions, ensuring that the largest output in
		// the remaining range is greater than LargestOutput.
		private static ReadOnlyMemory<Decomposition> PruneLexicographically(ReadOnlyMemory<Decomposition> orderedDecompositions, Money largetstOutputLowerBound)
		{
			var i = FindIndex(orderedDecompositions.Span, new Decomposition(largetstOutputLowerBound - 1L), new ReverseLexicographicalComparer());

			return orderedDecompositions[..i];
		}

		// Prune an array of decompositions, restricting to a range of total
		// values.
		// This is a private method, made internal so that it has unit tests.
		internal static ReadOnlyMemory<Decomposition> PruneByTotalValue(ReadOnlyMemory<Decomposition> byTotalValue, Money maximum, Money minimum)
		{
			var from = FindIndex(byTotalValue.Span, maximum);
			var to = FindIndex(byTotalValue.Span, minimum - 1L);
			return byTotalValue[from..to];
		}

		// Find an index for a given total value, or the insert where
		// it would be inserted.
		private static Index FindIndex(ReadOnlySpan<Decomposition> orderedDecompositions, Money target)
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
				// When BinarySearch does not find a match it returns the
				// bitwise negation of the insertion index.
				return ~i;
			}
			else
			{
				// BinarySearch doesn't necessarily return the first entry, so
				// do an additional backwards linear scan to find it.
				while (i > 0 && comparer.Compare(orderedDecompositions[i - 1], orderedDecompositions[i]) == 0)
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
					({ } right, { } left) => left.TotalValue.CompareTo(right.TotalValue)
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
					({ } right, { } left) => y.Outputs[0].CompareTo(x.Outputs[0])
				};
		}
	}
}
