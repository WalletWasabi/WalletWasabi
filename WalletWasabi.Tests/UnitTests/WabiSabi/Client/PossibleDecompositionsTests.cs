using System.Linq;
using NBitcoin;
using WalletWasabi.WabiSabi.Models.Decomposition;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.WabiSabi.Client
{
	public class PossibleDecompositionsTests
	{
		[Fact]
		public void TestPrune()
		{
			Assert.Equal(new Decomposition[] { }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { }, 0, 0).ToArray());
			Assert.Equal(new Decomposition[] { new() }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new() }, 0, 0).ToArray());
			Assert.Equal(new Decomposition[] { new(), new() }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(), new() }, 0, 0).ToArray());
			Assert.Equal(new Decomposition[] { }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(1) }, 0, 0).ToArray());
			Assert.Equal(new Decomposition[] { new(1) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(1) }, 2L, 0).ToArray());
			Assert.Equal(new Decomposition[] { new(1) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(1) }, 2L, 1L).ToArray());
			Assert.Equal(new Decomposition[] { }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(1) }, 2L, 2L).ToArray());
			Assert.Equal(new Decomposition[] { new(1) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(1) }, 1L, 0).ToArray());
			Assert.Equal(new Decomposition[] { new(1) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(1) }, 1L, 1L).ToArray());
			Assert.Equal(new Decomposition[] { new(2) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(2) }, 2L, 2L).ToArray());

			var combs = new Decomposition[] { new(2, 2), new(2, 1), new(2), new(1, 1), new(1), new() };
			Assert.Equal(new Decomposition[] { new(2), new(1, 1) }, CombinationsOfASize.PruneByTotalValue(combs, 2L, 2L).ToArray());
			Assert.Equal(new Decomposition[] { new(2, 1), new(2), new(1, 1), new(1) }, CombinationsOfASize.PruneByTotalValue(combs, 3L, 1L).ToArray());

			Assert.Equal(new Decomposition[] { new(4), new(3) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(6), new(4), new(3), new(1) }, 4L, 3L).ToArray());
			Assert.Equal(new Decomposition[] { new(4), new(4), new(3), new(3) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(4), new(4), new(3), new(3), new(1) }, 4L, 3L).ToArray());
			Assert.Equal(new Decomposition[] { new(4), new(4), new(3), new(3) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(6), new(4), new(4), new(3), new(3), new(1) }, 4L, 3L).ToArray());
			Assert.Equal(new Decomposition[] { new(4), new(3) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(6), new(4), new(3), new(1) }, 4L, 2L).ToArray());
			Assert.Equal(new Decomposition[] { new(4), new(3) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(6), new(4), new(3), new(1) }, 5L, 3L).ToArray());
			Assert.Equal(new Decomposition[] { new(4), new(3), new(3) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(6), new(4), new(3), new(3), new(1) }, 5L, 3L).ToArray());
			Assert.Equal(new Decomposition[] { new(4), new(4), new(3), new(3) }, CombinationsOfASize.PruneByTotalValue(new Decomposition[] { new(6), new(4), new(4), new(3), new(3), new(1) }, 5L, 3L).ToArray());
		}

		[Fact]
		public void TestCombinations()
		{
			Assert.Equal(
				new Decomposition[] { },
				PossibleDecompositions.Generate(new Money[] { 1L }, 0, 0, 1).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(1) },
				PossibleDecompositions.Generate(new Money[] { 1L }, 1L, 0, 1).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(1) },
				PossibleDecompositions.Generate(new Money[] { 1L }, 1L, 0, 2).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(1, 1), new(1) },
				PossibleDecompositions.Generate(new Money[] { 1L }, 2L, 0, 2).GetByTotalValue());

			var denominations = new Money[] { 1L, 2L, 4L };

			Assert.Equal(
				new Decomposition[] { new(1) },
				PossibleDecompositions.Generate(denominations, 1L, 0, 3).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(2), new(1, 1), new(1) },
				PossibleDecompositions.Generate(denominations, 2L, 0, 3).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(2, 1), new(1, 1, 1), new(2), new(1, 1) },
				PossibleDecompositions.Generate(denominations, 3L, 2L, 3).GetByTotalValue());

			Assert.Equal(
				new Decomposition[]
				{
					new(4, 4, 4),
					new(4, 4, 2),
					new(4, 4, 1),
					new(4, 4),
					new(4, 2, 2),
					new(4, 2, 1),
					new(4, 2),
					new(4, 1, 1),
					new(2, 2, 2),
					new(4, 1),
					new(2, 2, 1),
					new(4),
					new(2, 2),
					new(2, 1, 1),
					new(2, 1),
					new(1, 1, 1),
					new(2),
					new(1, 1),
					new(1),
				},
				PossibleDecompositions.Generate(denominations, 12L, 0L, 3).GetByTotalValue());
		}

		[Fact]
		public void TestFeeRate()
		{
			var denominations = new Money[] { 1L, 2L, 4L };

			Assert.Equal(
				new Decomposition[]
				{
					new(4, 4, 1),
					new(4, 4),
					new(4, 2, 2),
				},
				PossibleDecompositions.Generate(denominations, 12L, 0L, 3).GetByTotalValue(
					maximumEffectiveCost: 12,
					minimumTotalValue: 8,
					feeRate: new FeeRate(1m),
					vsizePerOutput: 1));
		}

		[Fact]
		public void TestLargeDecompositions()
		{
			Assert.Equal(
				new Decomposition[]
				{
					new(8, 1),
					new(4, 4, 1),
					new(4, 2, 2, 1),
					new(4, 2, 1, 1, 1),
					new(2, 2, 2, 2, 1),
					new(4, 1, 1, 1, 1, 1),
					new(2, 2, 2, 1, 1, 1),
					new(2, 2, 1, 1, 1, 1, 1),
					new(2, 1, 1, 1, 1, 1, 1, 1),
					new(8),
					new(4, 4),
					new(4, 2, 2),
					new(4, 2, 1, 1),
					new(2, 2, 2, 2),
					new(4, 1, 1, 1, 1),
					new(2, 2, 2, 1, 1),
					new(2, 2, 1, 1, 1, 1),
					new(2, 1, 1, 1, 1, 1, 1),
					new(1, 1, 1, 1, 1, 1, 1, 1),
					new(4, 2, 1),
					new(4, 1, 1, 1),
					new(2, 2, 2, 1),
					new(2, 2, 1, 1, 1),
					new(2, 1, 1, 1, 1, 1),
					new(1, 1, 1, 1, 1, 1, 1),
					new(4, 2),
					new(4, 1, 1),
					new(2, 2, 2),
					new(2, 2, 1, 1),
					new(2, 1, 1, 1, 1),
					new(1, 1, 1, 1, 1, 1),
					new(4, 1),
					new(2, 2, 1),
					new(2, 1, 1, 1),
					new(1, 1, 1, 1, 1),
					new(4),
					new(2, 2),
					new(2, 1, 1),
					new(1, 1, 1, 1),
					new(2, 1),
					new(1, 1, 1),
					new(2),
					new(1, 1),
					new(1),
				},
				PossibleDecompositions.Generate(Enumerable.Range(0, 8).Select(x => new Money(1L << x)), 9L, 0, 8).GetByTotalValue());
		}

		[Fact]
		public void TestComplexDecompositions()
		{
			Assert.Equal(
				new Decomposition[] { new(128, 64, 32, 16, 8, 4, 2, 1) },
				PossibleDecompositions.Generate(Enumerable.Range(0, 8).Select(x => new Money(1L << x)), 255L, 255L, 8).GetByTotalValue());

			Assert.Equal(
				new Decomposition[]
				{
					new(1<<30),
					new(1<<29, 1<<29),
					new(1<<29, 1<<28, 1<<28),
					new(1<<29, 1<<28, 1<<27, 1<<27),
					new(1<<28, 1<<28, 1<<28, 1<<28),
					new(1<<29, 1<<28, 1<<27, 1<<26, 1<<26),
					new(1<<29, 1<<27, 1<<27, 1<<27, 1<<27),
					new(1<<28, 1<<28, 1<<28, 1<<27, 1<<27),
				},
				PossibleDecompositions.Generate(Enumerable.Range(0, 63).Select(x => new Money(1L << x)), 1L << 30, 1L << 30, 5).GetByTotalValue());

			Assert.Equal(
				StandardDenomination.Values.Select(v => new Decomposition(v)).OrderByDescending(x => x),
				PossibleDecompositions.Generate(StandardDenomination.Values, StandardDenomination.Values.Max()!, 0L, 1).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(1) },
				PossibleDecompositions.Generate(StandardDenomination.Values, 1L, 1L, 1).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(2) },
				PossibleDecompositions.Generate(StandardDenomination.Values, 2L, 2L, 1).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(2), new(1, 1) },
				PossibleDecompositions.Generate(StandardDenomination.Values, 2L, 2L, 2).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(5) },
				PossibleDecompositions.Generate(StandardDenomination.Values, 5L, 5L, 1).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { },
				PossibleDecompositions.Generate(StandardDenomination.Values, 7L, 7L, 1).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(5), new(4, 1), new(3, 2) },
				PossibleDecompositions.Generate(StandardDenomination.Values, 5L, 5L, 2).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(6, 1), new(5, 2), new(4, 3) },
				PossibleDecompositions.Generate(StandardDenomination.Values, 7L, 7L, 2).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(10), new(9, 1), new(8, 2), new(6, 4), new(5, 5) },
				PossibleDecompositions.Generate(StandardDenomination.Values, 10L, 10L, 2).GetByTotalValue());

			Assert.Equal(
				new Decomposition[] { new(1 << 20, 1_000_000) },
				PossibleDecompositions.Generate(StandardDenomination.Values, (1_000_000L + (1L << 20)), (1_000_000L + (1L << 20)), 2).GetByTotalValue());

			Assert.Equal(
				new Decomposition[]
				{
					new(1<<20, 1_000_000),
					new(1<<20, 500_000, 500_000),
					new(1_000_000, 1<<19, 1<<19),
					new(1_000_000, 1<<19, 1<<18, 1<<18),
					new(1<<19, 1<<19, 500_000, 500_000),
				},
				PossibleDecompositions.Generate(StandardDenomination.Values, (1_000_000L + (1L << 20)), (1_000_000L + (1L << 20)), 4).GetByTotalValue());

			// This is a bit like a regression test, in that it should become
			// annoyingly slow if pruning doesn't work. Increase the
			// precomputation to maxOutputs = 6 for a stronger effect.
			Assert.Equal(
				new Decomposition[]
				{
					new(1<<20, 1_000_000),
					new(1<<20, 500_000, 500_000),
					new(1_000_000, 1<<19, 1<<19),
					new(1_000_000, 1<<19, 1<<18, 1<<18),
					new(1<<19, 1<<19, 500_000, 500_000),
				},
				PossibleDecompositions.Generate(StandardDenomination.Values, (1_000_000L + (1L << 20)), (1_000_000L + (1L << 20)), 5).GetByTotalValue(maxOutputs: 4));
		}
	}
}
