using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.BranchNBound;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	/// <summary>
	/// Tests for <see cref="BranchAndBound"/> class.
	/// </summary>
	public class BranchAndBoundTests
	{
		private Random Random { get; } = new();

		[Fact]
		public void RandomizedTest()
		{
			List<long> inputValues = GenerateList();
			BranchAndBound selector = new(inputValues);
			long target = 100000000;

			bool successful = selector.TryGetExactMatch(target, out List<long>? selectedValues);

			Assert.True(successful);
			Assert.NotNull(selectedValues);
			Assert.Equal(target, selectedValues!.Sum());
		}

		[Fact]
		public void SimpleSelectionTest()
		{
			List<long> inputValues = new() { 120000, 100000, 100000, 50000, 40000 };
			BranchAndBound selector = new(inputValues);
			List<long> expectedCoins = new() { 100000, 50000, 40000 };
			long target = 190000;

			bool wasSuccessful = selector.TryGetExactMatch(target, out List<long>? selectedCoins);

			Assert.True(wasSuccessful);
			Assert.NotNull(selectedCoins);
			Assert.Equal(expectedCoins, selectedCoins);
		}

		[Fact]
		public void CanSelectEveryCoin()
		{
			List<long> inputValues = new() { 120000, 100000, 100000 };
			BranchAndBound selector = new(inputValues);
			List<long> expectedCoins = new() { 120000, 100000, 100000 };
			long target = 320000;

			bool wasSuccessful = selector.TryGetExactMatch(target, out List<long>? selectedCoins);

			Assert.True(wasSuccessful);
			Assert.NotNull(selectedCoins);
			Assert.Equal(expectedCoins, selectedCoins);
		}

		[Fact]
		public void TargetIsBiggerThanBalance()
		{
			List<long> inputValues = GenerateList();
			BranchAndBound selector = new(inputValues);
			long target = 11111111111111111;

			bool wasSuccessful = selector.TryGetExactMatch(target, out List<long>? selectedCoins);

			Assert.False(wasSuccessful);
			Assert.Null(selectedCoins);
		}

		[Fact]
		public void ReturnNullIfNoExactMatchFoundTest()
		{
			List<long> inputValues = new() { 120000, 100000, 100000, 50000, 40000 };
			BranchAndBound selector = new(inputValues);
			long target = 300000;

			bool wasSuccessful = selector.TryGetExactMatch(target, out List<long>? selectedCoins);

			Assert.False(wasSuccessful);
			Assert.Null(selectedCoins);
		}

		private List<long> GenerateList(int count = 1000)
		{
			List<long> values = new();

			for (int i = 0; i < 1000; i++)
			{
				values.Add(Random.Next(1000, 99999999));
			}
			return values;
		}
	}
}
