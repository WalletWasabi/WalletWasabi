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
			// and 1 output that equals to two others output, then its anonset should be 2.
			// Previously it was 3, but that's only against blockchain analysis,
			// against any participant of the coinjoin it should be 2.
			GetNewServices(out KeyManager keyManager, out CoinsRegistry registry, out AnonymityEstimator estimator);
			var inputCoin = GetNewCoin(keyManager, registry, Money.Coins(1), 1);
			Transaction tx = GetNewInputTransaction(inputCoin, 10);
			AddOwnOutput(tx, Money.Coins(0.999m), keyManager);
			AddRandomOutput(tx, Money.Coins(0.999m));
			AddRandomOutput(tx, Money.Coins(0.999m));

			var anonsets = estimator.EstimateAnonymitySets(tx);

			var result = Assert.Single(anonsets);
			var index = result.Key;
			var anonset = result.Value;
			Assert.Equal(0u, index);
			Assert.Equal(2, anonset);
		}

		[Fact]
		public void TwoParticipantCoinjoin()
		{
			// In a transaction where we have one input with anonset 1
			// and 1 output that equals to one other output, then its anonset should be less than 2.
			GetNewServices(out KeyManager keyManager, out CoinsRegistry registry, out AnonymityEstimator estimator);
			var inputCoin = GetNewCoin(keyManager, registry, Money.Coins(1), 1);
			Transaction tx = GetNewInputTransaction(inputCoin, 10);
			AddOwnOutput(tx, Money.Coins(0.999m), keyManager);
			AddRandomOutput(tx, Money.Coins(0.999m));

			var anonsets = estimator.EstimateAnonymitySets(tx);

			var result = Assert.Single(anonsets);
			var index = result.Key;
			var anonset = result.Value;
			Assert.Equal(0u, index);
			Assert.True(anonset < 2);
		}

		[Fact]
		public void DoesntCalculateForNoOwnOutputs()
		{
			// When we have no output in a tx, anonsets aren't calculated.
			GetNewServices(out KeyManager keyManager, out CoinsRegistry registry, out AnonymityEstimator estimator);
			var inputCoin = GetNewCoin(keyManager, registry, Money.Coins(1), 1);
			Transaction tx = GetNewInputTransaction(inputCoin, 10);
			AddRandomOutput(tx, Money.Coins(0.999m));

			var anonsets = estimator.EstimateAnonymitySets(tx);

			Assert.Empty(anonsets);
		}

		[Fact]
		public void AnonsetRetainedMaybeIncreasedBitBySelfSpend()
		{
			// Anonset gets retained for a single input and a single output tx.
			// Maybe it should be increased a bit because of deniability?
			GetNewServices(out KeyManager keyManager, out CoinsRegistry registry, out AnonymityEstimator estimator);
			var inputCoin = GetNewCoin(keyManager, registry, Money.Coins(1), 10);
			Transaction tx = GetNewInputTransaction(inputCoin, 0);
			AddOwnOutput(tx, Money.Coins(0.999m), keyManager);

			var anonsets = estimator.EstimateAnonymitySets(tx);

			var anonset = Assert.Single(anonsets).Value;
			Assert.True(anonset > 10);
			Assert.True(anonset < 11);
		}

		[Fact]
		public void ChangeAnonsetResets()
		{
			// In a normal tx, the change must get anonset 1, because we exposed it already.
			GetNewServices(out KeyManager keyManager, out CoinsRegistry registry, out AnonymityEstimator estimator);
			var inputCoin = GetNewCoin(keyManager, registry, Money.Coins(1), 10);
			Transaction tx = GetNewInputTransaction(inputCoin, 0);
			AddOwnOutput(tx, Money.Coins(0.999m), keyManager);
			AddRandomOutput(tx, Money.Coins(0.999m));

			var anonsets = estimator.EstimateAnonymitySets(tx);

			var anonset = Assert.Single(anonsets).Value;
			Assert.True(anonset < 2);
		}

		[Fact]
		public void InputMergeIsPunished()
		{
			// Merging inputs results in lower anonsets.
			GetNewServices(out KeyManager keyManager, out CoinsRegistry registry, out AnonymityEstimator estimator);
			var inputCoin1 = GetNewCoin(keyManager, registry, Money.Coins(1), 10);
			var inputCoin2 = GetNewCoin(keyManager, registry, Money.Coins(1), 10);
			Transaction tx = GetNewInputTransaction(new[] { inputCoin1, inputCoin2 }, 0);
			AddOwnOutput(tx, Money.Coins(0.999m), keyManager);

			var anonsets = estimator.EstimateAnonymitySets(tx);

			var anonset = Assert.Single(anonsets).Value;
			Assert.True(anonset > 1);
			Assert.True(anonset < 10);
		}

		[Fact]
		public void InputMergeIsPunishedMore()
		{
			// The more inputs merged, the lower the anonset is.
			GetNewServices(out KeyManager keyManager, out CoinsRegistry registry, out AnonymityEstimator estimator);
			var inputCoin1 = GetNewCoin(keyManager, registry, Money.Coins(1), 10);
			var inputCoin2 = GetNewCoin(keyManager, registry, Money.Coins(1), 10);
			var inputCoin3 = GetNewCoin(keyManager, registry, Money.Coins(1), 10);
			Transaction tx1 = GetNewInputTransaction(new[] { inputCoin1, inputCoin2 }, 0);
			Transaction tx2 = GetNewInputTransaction(new[] { inputCoin1, inputCoin2, inputCoin3 }, 0);
			AddOwnOutput(tx1, Money.Coins(0.999m), keyManager);
			AddOwnOutput(tx2, Money.Coins(0.999m), keyManager);

			var anonsets1 = estimator.EstimateAnonymitySets(tx1);
			var anonsets2 = estimator.EstimateAnonymitySets(tx2);

			var anonset1 = Assert.Single(anonsets1).Value;
			var anonset2 = Assert.Single(anonsets2).Value;
			Assert.True(anonset1 > anonset2);
		}

		[Fact]
		public void LargeCoinjoinMergeIsPunishedLess()
		{
			// Don't punish input merge in a large coinjoin as much as in a normal transaction.
			GetNewServices(out KeyManager keyManager, out CoinsRegistry registry, out AnonymityEstimator estimator);
			var inputCoin1 = GetNewCoin(keyManager, registry, Money.Coins(1), 10);
			var inputCoin2 = GetNewCoin(keyManager, registry, Money.Coins(1), 10);
			Transaction tx1 = GetNewInputTransaction(new[] { inputCoin1, inputCoin2 }, 0);
			Transaction tx2 = GetNewInputTransaction(new[] { inputCoin1, inputCoin2, }, 100);
			AddOwnOutput(tx1, Money.Coins(0.999m), keyManager);
			AddOwnOutput(tx2, Money.Coins(0.999m), keyManager);
			AddRandomOutput(tx1, Money.Coins(0.1m));
			AddRandomOutput(tx2, Money.Coins(0.1m));
			AddRandomOutput(tx2, Money.Coins(0.1m));
			AddRandomOutput(tx2, Money.Coins(0.1m));

			var anonsets1 = estimator.EstimateAnonymitySets(tx1);
			var anonsets2 = estimator.EstimateAnonymitySets(tx2);

			var anonset1 = Assert.Single(anonsets1).Value;
			var anonset2 = Assert.Single(anonsets2).Value;
			Assert.True(anonset1 < anonset2);
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

		private static Transaction GetNewInputTransaction(SmartCoin inputCoin, int randomInputs)
		{
			return GetNewInputTransaction(new[] { inputCoin }, randomInputs);
		}

		private static Transaction GetNewInputTransaction(IEnumerable<SmartCoin> inputCoins, int randomInputs)
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
