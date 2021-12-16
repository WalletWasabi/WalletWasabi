using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.BranchNBound;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class BranchAndBoundSelectionTests
	{
		private Random Random { get; } = new();

		[Fact]
		public void RandomizedTest()
		{
			List<Money> utxos = GenList();
			BranchAndBound selector = new(utxos);
			ulong target = Money.Satoshis(100000000);

			bool successful = selector.TryGetExactMatch(target, out List<Money> selectedCoins);

			Assert.True(successful);
			Assert.NotNull(selectedCoins);
			Assert.Equal(target, (ulong)selector.CalcEffectiveValue(selectedCoins));
		}

		[Fact]
		public void SimpleSelectionTest()
		{
			List<Money> utxos = new() { Money.Satoshis(120000), Money.Satoshis(100000), Money.Satoshis(100000), Money.Satoshis(50000), Money.Satoshis(40000) };
			BranchAndBound selector = new(utxos);
			List<Money> expectedCoins = new() { Money.Satoshis(100000), Money.Satoshis(50000), Money.Satoshis(40000) };
			Money target = Money.Satoshis(190000);

			bool wasSuccessful = selector.TryGetExactMatch(target, out List<Money> selectedCoins);

			Assert.True(wasSuccessful);
			Assert.NotNull(selectedCoins);
			Assert.Equal(expectedCoins, selectedCoins);
		}

		private List<Money> GenList()
		{
			List<Money> availableCoins = new();
			for (int i = 0; i < 1000; i++)
			{
				availableCoins.Add((ulong)Random.Next((int)Money.Satoshis(1000), (int)Money.Satoshis(99999999)));
			}
			return availableCoins;
		}
	}
}
