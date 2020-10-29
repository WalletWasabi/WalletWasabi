using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.Analysis.AnonymityEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class AnonymityEstimationTests
	{
		[Fact]
		public void BasicAnonsetCalculationTest()
		{
			// In a transaction where we have one input with anonset 1
			// and 1 output that equals to 10 other outputs, then its anonset should be 10.
			var km = KeyManager.CreateNew(out _, "");
			var inputKey = km.GenerateNewKey("", KeyState.Used, false);
			var c = new SmartCoin(Common.GetRandomUint256(), 0, inputKey.P2wpkhScript, Money.Coins(1), new OutPoint[] { new OutPoint(uint256.Zero, 0) }, Height.Mempool, false, 1);
			var registry = new CoinsRegistry(1);
			registry.TryAdd(c);
			var txStore = new AllTransactionStore(Common.GetWorkDir(), Network.Main);
			var estimator = new AnonymityEstimator(registry, txStore, km, Money.Satoshis(1));
			var tx = Transaction.Create(Network.Main);
			tx.Inputs.Add(c.OutPoint);
			for (int i = 1; i < 10; i++)
			{
				tx.Inputs.Add(new OutPoint(Common.GetRandomUint256(), i));
			}
			tx.Outputs.Add(Money.Coins(0.999m), km.GenerateNewKey("", KeyState.Clean, true).P2wpkhScript);
			for (int i = 0; i < 9; i++)
			{
				tx.Outputs.Add(Money.Coins(0.999m), new Key().PubKey.WitHash.ScriptPubKey);
			}

			var anonsets = estimator.EstimateAnonymitySets(tx);

			var result = Assert.Single(anonsets);
			var index = result.Key;
			var anonset = result.Value;
			Assert.Equal(0u, index);
			Assert.Equal(10, anonset);
		}
	}
}
