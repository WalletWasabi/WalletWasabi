using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
		public void BasicAnonsetCalculation()
		{
			// In a transaction where we have one input with anonset 1
			// and 1 output that equals to one other output, then its anonset should be 2.
			GetNewServices(out KeyManager keyManager, out CoinsRegistry registry, out AnonymityEstimator estimator);
			var inputCoin = GetNewCoin(keyManager, registry, Money.Coins(1), 1);
			Transaction tx = GetNewTransaction(inputCoin, 10);
			AddOwnOutput(tx, Money.Coins(0.999m), keyManager);
			AddRandomOutput(tx, Money.Coins(0.999m));

			var anonsets = estimator.EstimateAnonymitySets(tx);

			var result = Assert.Single(anonsets);
			var index = result.Key;
			var anonset = result.Value;
			Assert.Equal(0u, index);
			Assert.Equal(2, anonset);
		}

		[Fact]
		public void DoesntCalculateForNoOwnOutputs()
		{
			// When we have no output in a tx, anonsets aren't calculated.
			GetNewServices(out KeyManager keyManager, out CoinsRegistry registry, out AnonymityEstimator estimator);
			var inputCoin = GetNewCoin(keyManager, registry, Money.Coins(1), 1);
			Transaction tx = GetNewTransaction(inputCoin, 10);
			AddRandomOutput(tx, Money.Coins(0.999m));

			var anonsets = estimator.EstimateAnonymitySets(tx);

			Assert.Empty(anonsets);
		}

		private void AddRandomOutput(Transaction tx, Money value)
		{
			tx.Outputs.Add(value, new Key().PubKey.WitHash.ScriptPubKey);
		}

		private void AddOwnOutput(Transaction tx, Money value, KeyManager keyManager)
		{
			tx.Outputs.Add(value, keyManager.GenerateNewKey("", KeyState.Clean, true).P2wpkhScript);
		}

		private static SmartCoin GetNewCoin(KeyManager keyManager, CoinsRegistry registry, Money value, int anonset)
		{
			var coin = new SmartCoin(Common.GetRandomUint256(), 0, keyManager.GenerateNewKey("", KeyState.Used, false).P2wpkhScript, value, new OutPoint[] { new OutPoint(Common.GetRandomUint256(), 0) }, Height.Mempool, false, anonset);
			registry.TryAdd(coin);
			return coin;
		}

		private static void GetNewServices(out KeyManager keyManager, out CoinsRegistry registry, out AnonymityEstimator estimator, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
		{
			keyManager = KeyManager.CreateNew(out _, "");
			registry = new CoinsRegistry(1);
			var txStore = new AllTransactionStore(Common.GetWorkDir(callerFilePath, callerMemberName), Network.Main);
			estimator = new AnonymityEstimator(registry, txStore, keyManager, Money.Satoshis(1));
		}

		private static Transaction GetNewTransaction(SmartCoin inputCoin, int randomInputs)
		 => GetNewTransaction(new[] { inputCoin }, randomInputs);

		private static Transaction GetNewTransaction(IEnumerable<SmartCoin> inputCoins, int randomInputs)
		{
			var tx = Transaction.Create(Network.Main);
			foreach (var inputCoin in inputCoins)
			{
				tx.Inputs.Add(inputCoin.OutPoint);
			}
			var inputCoinsCount = inputCoins.Count();
			for (int i = inputCoinsCount; i < inputCoinsCount + randomInputs; i++)
			{
				tx.Inputs.Add(new OutPoint(Common.GetRandomUint256(), i));
			}

			return tx;
		}
	}
}
