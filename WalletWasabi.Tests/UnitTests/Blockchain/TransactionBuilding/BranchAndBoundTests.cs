using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionBuilding.BnB;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.TransactionBuilding;

/// <summary>
/// Tests for <see cref="BranchAndBound"/> class.
/// </summary>
public class BranchAndBoundTests
{
	/// <summary>Tests that a best found selection is found when an exact solution does not exist.</summary>
	[Fact]
	public void MoreSelection_NoInputCosts()
	{
		long[] inputValues = new long[] { 11, 7, 5, 3, 2 }; // Sum is 28.
		long[] inputCosts = new long[] { 0, 0, 0, 0, 0 }; // No input costs. Idealized.
		long target = 27; // Target that we cannot get as a sum of input values.

		BranchAndBound algorithm = new();
		MoreSelectionStrategy strategy = new(target, inputValues, inputCosts);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);
		Assert.Equal(new long[] { 11, 7, 5, 3, 2 }, strategy.GetBestSelectionFound());
	}

	[Fact]
	public void MoreSelection_ExactMatchIsAlsoCheapest()
	{
		long[] inputValues = new long[] { 35, 17, 10, 5, 3, 2 };
		long[] inputCosts = new long[] { 1, 5, 1, 1, 1, 1 };

		long target = 27;

		BranchAndBound algorithm = new();
		MoreSelectionStrategy strategy = new(target, inputValues, inputCosts);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);

		long[] actualSelection = strategy.GetBestSelectionFound()!;
		Assert.NotNull(actualSelection);

		Assert.Equal(new long[] { 17, 10 }, actualSelection);
	}

	[Fact]
	public void MoreSelection_ExactMatchIsExpensive()
	{
		long[] inputValues = new long[] { 35, 17, 10, 5, 3, 2 };

		// Make the second input very expensive to spend so that it is not selected (not likely in reality).
		long[] inputCosts = new long[] { 1, 1000, 1, 1, 1, 1 };

		long target = 27; // Target that we cannot get as a sum of input values.

		BranchAndBound algorithm = new();
		MoreSelectionStrategy strategy = new(target, inputValues, inputCosts);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);

		// Selection (17, 10) is actually more expensive: 17 + 10 + 1000 + 1 = 1018.
		// Whereas (35) costs us 35 + 1 = 36.
		long[] actualSelection = strategy.GetBestSelectionFound()!;
		Assert.NotNull(actualSelection);

		Assert.Equal(new long[] { 35 }, actualSelection);
	}

	/// <summary>
	/// The goal of the test is to verify that we prune branches at points when it is clear that
	/// the sum of remaining coins is less than what we need to hit our target.
	/// </summary>
	/// <remarks>
	/// This is especially important for cases where user has many coins.
	/// <para>If the optimization is not in place, you should observe that this test does not really finish fast.</para>
	/// </remarks>
	[Fact]
	public void MoreSelection_RemainingAmountOptimization()
	{
		List<long> inputValues = new();
		inputValues.Add(1_000_000);

		for (int i = 0; i < 1000; i++)
		{
			inputValues.Add(1);
		}

		// All inputs cost the same.
		long[] inputCosts = inputValues.Select(x => 1L).ToArray();
		
		long target = 999_999; // Target that we cannot get as a sum of input values.

		BranchAndBound algorithm = new();
		MoreSelectionStrategy strategy = new(target, inputValues.ToArray(), inputCosts);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);

		// Assert that we get expected best solution.
		long[] actualSelection = strategy.GetBestSelectionFound()!;
		Assert.NotNull(actualSelection);

		Assert.Equal(new long[] { 1_000_000 }, actualSelection);
	}
	
	[Fact]
	public void LesserSelection_NoInputCosts()
	{
		long[] inputValues = new long[] { 35, 17, 10, 5, 3, 2 };
		long[] inputCosts = new long[] { 0, 0, 0, 0, 0, 0 };

		long target = 26;

		BranchAndBound algorithm = new();
		LessSelectionStrategy strategy = new(target, inputValues, inputCosts);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);

		long[] actualSelection = strategy.GetBestSelectionFound()!;
		Assert.NotNull(actualSelection);

		// Target 26, closest match is 25. (17, 5, 3)
		Assert.Equal(new long[] { 17, 5, 3 }, actualSelection);
	}

	[Fact]
	public void LesserSelection_WithInputCosts()
	{
		long[] inputValues = new long[] { 35, 17, 10, 5, 3, 2 };
		long[] inputCosts = new long[] { 1, 2, 1, 3, 1, 1 };

		long target = 32;

		BranchAndBound algorithm = new();
		LessSelectionStrategy strategy = new(target, inputValues, inputCosts);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);

		long[] actualSelection = strategy.GetBestSelectionFound()!;
		Assert.NotNull(actualSelection);

		// Target 32, closest match is 31: (17 + 2) + (5 + 3) + (3 + 1) = 31
		Assert.Equal(new long[] { 17, 5, 3 }, actualSelection);
	}
}
