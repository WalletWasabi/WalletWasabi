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
		[Fact]
		public void CanSelectCoinsWithBranchAndBoundTest()
		{
			var availableCoins = new List<ulong> { Money.Satoshis(12), Money.Satoshis(10), Money.Satoshis(1), Money.Satoshis(8), Money.Satoshis(11), Money.Satoshis(6), Money.Satoshis(10), Money.Satoshis(5), Money.Satoshis(4), Money.Satoshis(8), Money.Satoshis(15), Money.Satoshis(24), Money.Satoshis(1), Money.Satoshis(1) };
			var expectedCoins = new List<ulong>() { Money.Satoshis(12), Money.Satoshis(5) };

			ulong target = (ulong)new Random().Next(0, 130);
			ulong tolerance = 0;

			SendCoinSelector bab = new();
			Assert.True(bab.TryBranchAndBound(availableCoins, target, tolerance, out List<ulong> selectedCoins));
			Assert.Equal(target, bab.CalculateSum(selectedCoins));
		}
	}
}
