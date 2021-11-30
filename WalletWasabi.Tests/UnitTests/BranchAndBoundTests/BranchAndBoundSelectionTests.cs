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
			var availableCoins = new List<ulong> { Money.Satoshis(500), Money.Satoshis(900), Money.Satoshis(700), Money.Satoshis(1000) };
			var expectedCoins = new List<ulong>() { Money.Satoshis(900), Money.Satoshis(500) };
			ulong target = 1400;
			ulong tolerance = 0;

			SendCoinSelector bab = new();
			Assert.True(bab.TryBranchAndBound(availableCoins, target, tolerance, out List<ulong> selectedCoins));
			Assert.Equal(expectedCoins, selectedCoins);
		}
	}
}
