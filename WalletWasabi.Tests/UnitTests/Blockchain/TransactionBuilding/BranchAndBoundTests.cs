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
	/// Tests that sum of input values must be larger or equal to the target otherwise we end up searching all options in vain.
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

	/// <summary>Tests that best found solution is found when exact solution does not exist.</summary>
	[Fact]
	public void PruneByBest_NoExactSolution()
	{
		List<long> inputValues = new() { 2, 3, 5, 7, 11 }; // Sum is 28.
		long target = 27; // Target that we cannot get as a sum of input values.

		BranchAndBound algorithm = new(inputValues);
		BestSumStrategy strategy = new(target);
		bool wasSuccessful = algorithm.TryGetMatch(strategy, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);
		Assert.Equal(new long[] { 11, 7, 5, 3, 2 }, strategy.GetBestSumFound());
	}

	private List<long> GenerateListOfRandomValues(int count = 1000)
	{
		List<long> values = new();

		for (int i = 0; i < count; i++)
		{
			values.Add(Random.Next(1000, 99999999));
		}

		return values;
	}
}
