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
	private static Random Random { get; } = new();

	[Fact]
	public void InvalidInputs()
	{
		List<long> inputValues = new() { };
		ArgumentException argumentException = Assert.Throws<ArgumentException>(() => new BranchAndBound(inputValues));
		Assert.Equal("List is empty.", argumentException.Message);

		inputValues = new() { -1, 1, 1 };
		argumentException = Assert.Throws<ArgumentException>(() => new BranchAndBound(inputValues));
		Assert.Equal("Only strictly positive values are supported.", argumentException.Message);

		inputValues = new() { 1, 2, 3, 1 };
		argumentException = Assert.Throws<ArgumentException>(() => new BranchAndBound(inputValues));
		Assert.Equal("Input values must be sorted in descending order.", argumentException.Message);
	}

	[Fact]
	public void ExactMatch_RandomizedTest()
	{
		List<long> inputValues = GenerateListOfRandomValues();
		long target = 100_000_000;

		BranchAndBound algorithm = new(inputValues);
		ExactMatchStrategy strategy = new(target);
		bool successful = algorithm.TryGetMatch(strategy, out List<long>? selectedValues);

		Assert.True(successful);
		Assert.NotNull(selectedValues);
		Assert.Equal(target, selectedValues!.Sum());
	}

	[Fact]
	public void ExactMatch_SimpleSelectionTest()
	{
		List<long> inputValues = new() { 120_000, 100_000, 100_000, 50_000, 40_000 };
		List<long> expectedValues = new() { 100_000, 50_000, 40_000 };
		long target = 190_000;

		BranchAndBound algorithm = new(inputValues);
		ExactMatchStrategy strategy = new(target);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.True(wasSuccessful);
		Assert.NotNull(selectedCoins);
		Assert.Equal(expectedValues, selectedCoins);
	}

	[Fact]
	public void ExactMatch_CanSelectEveryCoin()
	{
		List<long> inputValues = new() { 120_000, 100_000, 100_000 };
		List<long> expectedValues = new() { 120_000, 100_000, 100_000 };
		long target = 320000;

		BranchAndBound algorithm = new(inputValues);
		ExactMatchStrategy strategy = new(target);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.True(wasSuccessful);
		Assert.NotNull(selectedCoins);
		Assert.Equal(expectedValues, selectedCoins);
	}

	/// <summary>
	/// Tests that sum of input values must be larger or equal to the target otherwise
	/// we end up searching all options in vain.
	/// </summary>
	[Fact]
	public void ExactMatch_TargetIsBiggerThanBalance()
	{
		List<long> inputValues = new();
		long target = 5_000;

		for (int i = 0; i < target - 1; i++)
		{
			inputValues.Add(1);
		}

		BranchAndBound algorithm = new(inputValues);
		ExactMatchStrategy strategy = new(target);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);
	}

	[Fact]
	public void ExactMatch_ReturnNullIfNoExactMatchFoundTest()
	{
		List<long> inputValues = new() { 120_000, 100_000, 100_000, 50_000, 40_000 };
		long target = 300000;

		BranchAndBound algorithm = new(inputValues);
		ExactMatchStrategy strategy = new(target);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);
	}

	/// <summary>Tests that a best found selection is found when an exact solution does not exist.</summary>
	[Fact]
	public void CheapestSelection_NoInputCosts()
	{
		List<long> inputValues = new() { 11, 7, 5, 3, 2 }; // Sum is 28.
		long[] inputCosts = new long[] { 0, 0, 0, 0, 0 }; // No input costs. Idealized.
		long target = 27; // Target that we cannot get as a sum of input values.

		BranchAndBound algorithm = new(inputValues);
		CheapestSelectionStrategy strategy = new(target, inputCosts);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);
		Assert.Equal(new long[] { 11, 7, 5, 3, 2 }, strategy.GetBestSelectionFound());
	}

	[Fact]
	public void CheapestSelection_ExactMatchIsAlsoCheapest()
	{
		List<long> inputValues = new() { 35, 17, 10, 5, 3, 2 };

		// Make the second input very expensive to spend so that it is not selected (not likely in reality).
		long[] inputCosts = new long[] { 1, 5, 1, 1, 1, 1 };

		long target = 27;

		BranchAndBound algorithm = new(inputValues);
		CheapestSelectionStrategy strategy = new(target, inputCosts);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);

		// There are multiple existing solutions.
		long[][] solutions = new long[][]  {
			new long[] { 17, 10 },
			new long[] { 17, 5, 3, 2 }
		};

		long[] actualSelection = strategy.GetBestSelectionFound()!;
		Assert.NotNull(actualSelection);
		Assert.True(solutions[0].SequenceEqual(actualSelection) || solutions[1].SequenceEqual(actualSelection));
	}

	[Fact]
	public void CheapestSelection_ExactMatchIsExpensive()
	{
		List<long> inputValues = new() { 35, 17, 10, 5, 3, 2 };

		// Make the second input very expensive to spend so that it is not selected (not likely in reality).
		long[] inputCosts = new long[] { 1, 1000, 1, 1, 1, 1 };

		long target = 27; // Target that we cannot get as a sum of input values.

		BranchAndBound algorithm = new(inputValues);
		CheapestSelectionStrategy strategy = new(target, inputCosts);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);

		// Selection (17, 10) is actually more expensive: 17 + 10 + 1000 + 1 = 1018.
		// Whereas (35) costs us 35 + 1 = 36.
		Assert.Equal(new long[] { 35 }, strategy.GetBestSelectionFound());
	}

	private List<long> GenerateListOfRandomValues(int count = 1000)
	{
		List<long> values = new();

		for (int i = 0; i < count; i++)
		{
			values.Add(Random.Next(1000, 99999999));
		}

		values = values.OrderByDescending(x => x).ToList();

		return values;
	}
}
