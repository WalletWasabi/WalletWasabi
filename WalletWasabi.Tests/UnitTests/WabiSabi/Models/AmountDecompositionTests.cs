using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Models.DecompositionAlgs;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models
{
	public class AmountDecompositionTests
	{
		[Fact]
		public void GreedyDecompositionTest()
		{
			var feeRate = new FeeRate(100m);
			var dustThreshold = Money.Coins(0.00001m);

			GreedyDecomposer greedyDecomposer = new(
				new Money[]
				{
					Money.Coins(2m),
					Money.Coins(3m),
					Money.Coins(4m)
				},
				dustThreshold,
				feeRate);

			var km = ServiceFactory.CreateKeyManager();

			var c1 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1));
			var c2 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(2));
			var c3 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(3));
			var c4 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(5));

			greedyDecomposer.Decompose(c1.Coin);
			greedyDecomposer.Decompose(c2.Coin);
			greedyDecomposer.Decompose(c3.Coin);
			greedyDecomposer.Decompose(c4.Coin);

			Assert.Equal(
				new Money[]
				{
					Money.Coins(0.99996900m),
					Money.Coins(2.00000000m),
					Money.Coins(3.00000000m),
					Money.Coins(4.00000000m),
				},
				greedyDecomposer.Decomposition
				);
		}
	}
}
