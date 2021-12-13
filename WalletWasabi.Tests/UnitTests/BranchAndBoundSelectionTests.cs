using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.BranchNBound;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class BranchAndBoundSelectionTests
	{
		private static Random Random = new Random();
		private static List<Money> AvailableCoins = GenList();

		private static List<Money> GenList()
		{
			List<Money> availableCoins = new List<Money>();
			for (int i = 0; i < 1000; i++)
			{
				availableCoins.Add((ulong)Random.Next((int)Money.Satoshis(1000), (int)Money.Satoshis(99999999)));
			}
			return availableCoins;
		}

		[Fact]
		public void CanSelectCoinsWithNewLogicRandomTest()
		{
			ulong target = Money.Satoshis(100000000);
			ulong maxTolerance = 500;
			ulong toleranceIncrement = 100;

			RecursiveCoinSelector selector = new();
			Assert.True(selector.TryBranchAndBound(AvailableCoins, target, maxTolerance, toleranceIncrement, out List<Money> selectedCoins));
			Assert.True(target + selector.Tolerance <= selector.CalcEffectiveValue(selectedCoins));
		}

		[Fact]
		public void NewLogicSimpleTest()
		{
			var utxos = new List<Money> { Money.Satoshis(12), Money.Satoshis(10), Money.Satoshis(10), Money.Satoshis(5), Money.Satoshis(4) };
			var expectedCoins = new List<Money> { Money.Satoshis(10), Money.Satoshis(5), Money.Satoshis(4) };
			ulong maxTolerance = 500;
			ulong toleranceIncrement = 100;
			ulong target = Money.Satoshis(19);

			RecursiveCoinSelector selector = new();
			Assert.True(selector.TryBranchAndBound(utxos, target, maxTolerance, toleranceIncrement, out List<Money> selectedCoins));
			Assert.Equal(expectedCoins, selectedCoins);
		}

		[Fact]
		public void NodesLogicSimpleTest()
		{
			var utxos = new List<Money> { Money.Satoshis(12), Money.Satoshis(10), Money.Satoshis(10), Money.Satoshis(5), Money.Satoshis(4) };
			var expectedCoins = new List<Money> { Money.Satoshis(10), Money.Satoshis(5), Money.Satoshis(4) };

			ulong target = Money.Satoshis(19);

			TreeBranchCoinSelector selector = new();
			Assert.True(selector.TryTreeLogic(utxos, target, out List<Money> selectedCoins));
			Assert.Equal(expectedCoins, selectedCoins);
		}

		[Fact]
		public void CanSelectCoinsWithOriginalRandomTest()
		{
			var selector = new BranchAndBound(AvailableCoins);
			ulong target = Money.Satoshis(100000000);

			var successful = selector.TryGetExactMatch(target, out List<Money> selectedCoins);

			Assert.True(successful);
			Assert.Equal(target, (ulong)selector.CalcEffectiveValue(selectedCoins));
		}

		[Fact]
		public void OriginalSimpleTest()
		{
			var utxos = new List<Money> { Money.Satoshis(12), Money.Satoshis(10), Money.Satoshis(10), Money.Satoshis(5), Money.Satoshis(4) };
			var selector = new BranchAndBound(utxos);
			var expectedCoins = new List<Money> { Money.Satoshis(10), Money.Satoshis(5), Money.Satoshis(4) };
			Money target = Money.Satoshis(19);

			var wasSuccessful = selector.TryGetExactMatch(target, out List<Money> selectedCoins);

			Assert.True(wasSuccessful);
			Assert.Equal(expectedCoins, selectedCoins);
		}
	}
}
