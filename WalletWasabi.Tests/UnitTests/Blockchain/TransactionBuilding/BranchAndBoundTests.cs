using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionBuilding;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.TransactionBuilding;

/// <summary>
/// Tests for <see cref="BranchAndBound"/> class.
/// </summary>
public class BranchAndBoundTests
{
	private static Random Random { get; } = new();

	[Fact]
	public void RandomizedTest()
	{
		List<long> inputValues = GenerateListOfRandomValues();
		BranchAndBound selector = new(inputValues);
		long target = 100_000_000;

		bool successful = selector.TryGetExactMatch(target, out List<long>? selectedValues);

		Assert.True(successful);
		Assert.NotNull(selectedValues);
		Assert.Equal(target, selectedValues!.Sum());
	}

	[Fact]
	public void SimpleSelectionTest()
	{
		List<long> inputValues = new() { 120_000, 100_000, 100_000, 50_000, 40_000 };
		BranchAndBound selector = new(inputValues);
		List<long> expectedValues = new() { 100_000, 50_000, 40_000 };
		long target = 190_000;

		bool wasSuccessful = selector.TryGetExactMatch(target, out List<long>? selectedCoins);

		Assert.True(wasSuccessful);
		Assert.NotNull(selectedCoins);
		Assert.Equal(expectedValues, selectedCoins);
	}

	[Fact]
	public void CanSelectEveryCoin()
	{
		List<long> inputValues = new() { 120_000, 100_000, 100_000 };
		BranchAndBound selector = new(inputValues);
		List<long> expectedValues = new() { 120_000, 100_000, 100_000 };
		long target = 320000;

		bool wasSuccessful = selector.TryGetExactMatch(target, out List<long>? selectedCoins);

		Assert.True(wasSuccessful);
		Assert.NotNull(selectedCoins);
		Assert.Equal(expectedValues, selectedCoins);
	}

	/// <summary>
	/// Tests that sum of input values must be larger or equal to the target otherwise we end up searching all options in vain.
	/// </summary>
	[Fact]
	public void TargetIsBiggerThanBalance()
	{
		long target = 5_000;
		List<long> inputValues = new();

		for (int i = 0; i < target - 1; i++)
		{
			inputValues.Add(1);
		}

		BranchAndBound selector = new(inputValues);
		bool wasSuccessful = selector.TryGetExactMatch(target, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);
	}

	[Fact]
	public void ReturnNullIfNoExactMatchFoundTest()
	{
		List<long> inputValues = new() { 120_000, 100_000, 100_000, 50_000, 40_000 };
		BranchAndBound selector = new(inputValues);
		long target = 300000;

		bool wasSuccessful = selector.TryGetExactMatch(target, out List<long>? selectedCoins);

		Assert.False(wasSuccessful);
		Assert.Null(selectedCoins);
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
