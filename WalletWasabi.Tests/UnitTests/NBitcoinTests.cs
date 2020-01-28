using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class NBitcoinTests
	{
		[Fact]
		public void DefaultPortsMatch()
		{
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultMainNetBitcoinCoreRpcPort, Network.Main.RPCPort);
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultTestNetBitcoinCoreRpcPort, Network.TestNet.RPCPort);
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultRegTestBitcoinCoreRpcPort, Network.RegTest.RPCPort);
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultMainNetBitcoinP2pPort, Network.Main.DefaultPort);
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultTestNetBitcoinP2pPort, Network.TestNet.DefaultPort);
			Assert.Equal(WalletWasabi.Helpers.Constants.DefaultRegTestBitcoinP2pPort, Network.RegTest.DefaultPort);
		}

		[Fact]
		public void DependencyTransactionsGraph()
		{
			//  tx0 -----+
			//           |
			//           +----+---> tx3
			//           |    |
			//  tx1 -----+    +---> tx4 ---> tx5 ---> tx6 ----+
			//                                                |
			//                                                +---> tx7
			//                                                |
			//  tx2 ------------------------------------------+

			var (tx0, c0) = CreateTransaction();
			var (tx1, c1) = CreateTransaction();
			var (tx2, c2) = CreateTransaction();
			var (tx3, _) = CreateTransaction(c0[0], c0[1], c1[0]);
			var (tx4, c4) = CreateTransaction(c0[2], c1[2]);
			var (tx5, c5) = CreateTransaction(c4[1], c4[2]);

			var (tx6, c6) = CreateTransaction(c5[0]);
			var (tx7, _) = CreateTransaction(c6[2], c2[0]);

			var graph = new[] { tx0, tx1, tx2, tx7, tx5, tx3, tx4, tx6 }.ToDependencyGraph();

			Assert.Equal(3, graph.Count());  // tx0, tx1 and tx2
			Assert.Equal(2, graph.First().Children.Count());  // tx0 has two children tx3 and tx4
			Assert.Equal(2, graph.Skip(1).First().Children.Count());  // tx1 has two children tx3 and tx4
			Assert.Single(graph.Last().Children);  // tx2 has only one children tx7
			Assert.Equal(2, graph.Last().Children.Single().Parents.Count());  // tx7 has two parents tx2 and tx6

			var txs = graph.OrderByDependency().ToArray();
			Assert.Equal(8, txs.Count());
			Assert.Equal(tx0, txs[0]);
			Assert.Equal(tx1, txs[1]);
			Assert.Equal(tx2, txs[2]);
			Assert.Equal(tx3, txs[3]);
			Assert.Equal(tx4, txs[4]);
			Assert.Equal(tx5, txs[5]);
			Assert.Equal(tx6, txs[6]);
			Assert.Equal(tx7, txs[7]);
		}

		private static (Transaction, Coin[]) CreateTransaction(params Coin[] coins)
		{
			if (coins is null || coins.Length == 0)
			{
				coins = Enumerable.Range(0, 10).Select(_ => new Coin(RandomUtils.GetUInt256(), 0u, Money.Coins(10), Script.Empty)).ToArray();
			}
			var tx = Network.RegTest.CreateTransaction();
			foreach (var coin in coins)
			{
				tx.Inputs.Add(coin.Outpoint, Script.Empty, WitScript.Empty);
			}
			tx.Outputs.Add(Money.Coins(3), Script.Empty);
			tx.Outputs.Add(Money.Coins(2), Script.Empty);
			tx.Outputs.Add(Money.Coins(1), Script.Empty);
			tx.PrecomputeHash(true, false);
			return (tx, tx.Outputs.AsCoins().ToArray());
		}
	}
}
