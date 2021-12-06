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
			for (int i = 0; i < 100; i++)
			{
				//availableCoins.Add(Money.Satoshis(1));
				availableCoins.Add((ulong)Random.Next((int)Money.Satoshis(250), (int)Money.Satoshis(10001)));
			}
			return availableCoins;
		}

		[Fact]
		public void CanSelectCoinsWithBranchAndBoundTest()
		{
			//Random random = new Random();
			var availableCoins = new List<ulong>() { Money.Satoshis(12), Money.Satoshis(10), Money.Satoshis(10), Money.Satoshis(5), Money.Satoshis(4) };
			//for (int i = 0; i < 100; i++)
			//{
			//	//availableCoins.Add(Money.Satoshis(1));
			//	availableCoins.Add((ulong)random.Next((int)Money.Satoshis(250), (int)Money.Satoshis(10001)));
			//}
			ulong target = 23;
			ulong maxTolerance = 5;
			ulong toleranceIncrement = 1;

			SendCoinSelector bab = new();
			Assert.True(bab.TryBranchAndBound(availableCoins, target, maxTolerance, toleranceIncrement, out var tolerance, out List<ulong> selectedCoins));
			Assert.Equal(target + tolerance, bab.CalculateSum(selectedCoins));
		}

		[Fact]
		public void CanSelectCoinsWithBranchNodesLogic()
		{
			//Random random = new Random();
			//var availableCoins = new List<ulong>();
			//for (int i = 0; i < 100; i++)
			//{
			//	//availableCoins.Add(Money.Satoshis(1));
			//	availableCoins.Add((ulong)random.Next((int)Money.Satoshis(250), (int)Money.Satoshis(10001)));
			//}
			ulong target = 25000;
			SendCoinSelector bab = new();
			Assert.True(bab.TryTreeLogic(AvailableCoins, target, out List<ulong> selectedCoins));
			Assert.Equal(target, bab.CalculateSum(selectedCoins));
		}

		[Fact]
		public void TryOGBranchAndBound()
		{
			var availableCoins = new List<ulong>() { Money.Satoshis(12), Money.Satoshis(10), Money.Satoshis(10), Money.Satoshis(5), Money.Satoshis(4) };
			ulong target = 20;
			ulong depth = 0;
			SendCoinSelector bab = new();
			//Assert.True(bab.TryOGBranchAndBound(depth, availableCoins, target))
		}

		[Fact]
		public void WillNotRunIntoStackOverflowException()
		{
			var availableCoins = new List<ulong>();
			for (int i = 0; i < 7000; i++)
			{
				availableCoins.Add(Money.Satoshis(1));
				//availableCoins.Add((ulong)random.Next((int)Money.Satoshis(250), (int)Money.Satoshis(99999999)));
			}
			ulong target = 7000;

			SendCoinSelector bab = new();
			Assert.True(bab.TryTreeLogic(availableCoins, target, out List<ulong> selectedCoins));
			Assert.Equal(target, bab.CalculateSum(selectedCoins));
		}

		private static ulong Sum(List<ulong> list)
		{
			ulong sum = 0;
			if (list is null)
			{
				return 0;
			}
			foreach (var item in list)
			{
				sum += item;
			}
			return sum;
		}
	}
}
