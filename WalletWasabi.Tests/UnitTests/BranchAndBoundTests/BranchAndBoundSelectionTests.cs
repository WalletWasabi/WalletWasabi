using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.BranchNBound;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BranchAndBoundTests
{
	public class BranchAndBoundSelectionTests
	{
		private static Random Random = new Random();
		private static List<ulong> AvailableCoins = GenList();

		private static List<ulong> GenList()
		{
			List<ulong> availableCoins = new List<ulong>();
			for (int i = 0; i < 1000; i++)
			{
				availableCoins.Add((ulong)Random.Next((int)Money.Satoshis(1000), (int)Money.Satoshis(99999999)));
			}
			return availableCoins;
		}

		[Fact]
		public void CanSelectCoinsWithNewLogicTest()
		{
			ulong target = 1000000000;
			ulong maxTolerance = 500;
			ulong toleranceIncrement = 100;

			SendCoinSelector bab = new();
			Assert.True(bab.TryBranchAndBound(AvailableCoins, target, maxTolerance, toleranceIncrement, out var tolerance, out List<ulong> selectedCoins));
			Assert.True(target + tolerance <= bab.CalculateSum(selectedCoins));
		}

		[Fact]
		public void CanSelectCoinsWithBranchNodesLogic()
		{
			ulong target = 1000000000;

			SendCoinSelector bab = new();
			Assert.True(bab.TryTreeLogic(AvailableCoins, target, out List<ulong> selectedCoins));
			Assert.Equal(target, bab.CalculateSum(selectedCoins));
		}

		[Fact]
		public void BranchLogicWillNotRunIntoStackOverflowExceptionTest()
		{
			ulong target = 1000000000;

			SendCoinSelector bab = new();
			Assert.True(bab.TryTreeLogic(AvailableCoins, target, out List<ulong> selectedCoins));
			Assert.Equal(target, bab.CalculateSum(selectedCoins));
		}

		[Fact]
		public void BnBSimpleTest()
		{
			var bnb = new SendCoinSelector();
			var utxos = new List<Money> { Money.Satoshis(12), Money.Satoshis(10), Money.Satoshis(10), Money.Satoshis(5), Money.Satoshis(4) };
			var expectedCoins = new List<Money> { Money.Satoshis(10), Money.Satoshis(5), Money.Satoshis(4) };
			Money target = Money.Satoshis(19);

			var wasSuccessful = bnb.TryGetExactMatch(target, utxos, out List<Money> selectedCoins);

			Assert.True(wasSuccessful);
			Assert.Equal(expectedCoins, selectedCoins);
		}

		[Fact]
		public void BnBRandomTest()
		{
			var bnb = new SendCoinSelector();
			var utxos = GenerateRandomCoinList();
			Money target = Money.Satoshis(100000);

			var successful = bnb.TryGetExactMatch(target, utxos, out List<Money> selectedCoins);

			Assert.True(successful);
		}

		private List<Money> GenerateRandomCoinList()
		{
			Random random = new();
			List<Money> availableCoins = new();
			for (int i = 0; i < 100; i++)
			{
				availableCoins.Add(random.Next((int)Money.Satoshis(250), (int)Money.Satoshis(100001)));
			}
			return availableCoins;
		}
	}
}
