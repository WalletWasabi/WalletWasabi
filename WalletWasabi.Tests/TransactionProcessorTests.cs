using NBitcoin;
using System;
using System.Linq;
using WalletWasabi.KeyManagement;
using WalletWasabi.Models;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests
{
	public class TransactionProcessorTests
	{
		[Fact]
		public void TransactionDoesNotCointainCoinsForTheWallet()
		{
			var transactionProcessor = CreateTransactionProcessor();

			// This transaction doesn't have any coin for the wallet. It is not relevant.
			var tx = CreateCreditingTransaction(new Key().PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m));

			var relevant = transactionProcessor.Process(tx);

			Assert.False(relevant);
			Assert.Empty(transactionProcessor.Coins);
			Assert.Empty(transactionProcessor.TransactionCache);
		}

		[Fact]
		public void SpendToLegacyScripts()
		{
			var transactionProcessor = CreateTransactionProcessor();
			var keys = transactionProcessor.KeyManager.GetKeys().ToArray();

			// A payment to a key under our control but using P2PKH script (legacy)
			var tx = CreateCreditingTransaction(keys.First().P2pkhScript, Money.Coins(1.0m));
			var relevant = transactionProcessor.Process(tx);

			Assert.False(relevant);
			Assert.Empty(transactionProcessor.Coins);
		}

		[Fact]
		public void UnconfirmedTransactionIsNotSegWit()
		{
			var transactionProcessor = CreateTransactionProcessor();

			// No segwit transaction. Ignore it.
			var tx = CreateCreditingTransaction(new Key().PubKey.Hash.ScriptPubKey, Money.Coins(1.0m));

			var relevant = transactionProcessor.Process(tx);

			Assert.False(relevant);
			Assert.Empty(transactionProcessor.Coins);
			Assert.Empty(transactionProcessor.TransactionCache);
		}

		[Fact]
		public void ConfirmedTransactionIsNotSegWit()
		{
			var transactionProcessor = CreateTransactionProcessor();

			// No segwit transaction. Ignore it.
			var tx = CreateCreditingTransaction(new Key().PubKey.Hash.ScriptPubKey, Money.Coins(1.0m), isConfirmed: true);
			transactionProcessor.TransactionHashes.Append(tx.GetHash()); // This transaction was already seen before

			var relevant = transactionProcessor.Process(tx);

			Assert.False(relevant);
			Assert.Empty(transactionProcessor.Coins);
			Assert.Empty(transactionProcessor.TransactionCache);
			Assert.Empty(transactionProcessor.TransactionHashes);
		}

		[Fact]
		public void UpdateTransactionHeightAfterConfirmation()
		{
			var transactionProcessor = CreateTransactionProcessor();

			// An unconfirmed segwit transaction for us
			var key = transactionProcessor.KeyManager.GetKeys().First();

			var tx = CreateCreditingTransaction(key.PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m));
			transactionProcessor.TransactionHashes.Append(tx.GetHash()); // This transaction was already seen before
			transactionProcessor.Process(tx);

			var cachedTx = Assert.Single(transactionProcessor.TransactionCache);
			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal(Height.Mempool, cachedTx.Height);
			Assert.Equal(Height.Mempool, coin.Height);

			// Now it is confirmed
			var blockHeight = new Height(77551);
			tx = new SmartTransaction(tx.Transaction, blockHeight);
			var relevant = transactionProcessor.Process(tx);

			Assert.True(relevant);
			Assert.Single(transactionProcessor.Coins);
			cachedTx = Assert.Single(transactionProcessor.TransactionCache);
			Assert.NotEqual(Height.Mempool, cachedTx.Height);
			coin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal(blockHeight, coin.Height);

			Assert.Empty(transactionProcessor.TransactionHashes);
		}

		[Fact]
		public void IgnoreDoubleSpend()
		{
			var transactionProcessor = CreateTransactionProcessor();

			var keys = transactionProcessor.KeyManager.GetKeys().ToArray();

			// An unconfirmed segwit transaction for us
			var tx = CreateCreditingTransaction(keys[0].PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m));
			transactionProcessor.Process(tx);

			var createdCoin = tx.Transaction.Outputs.AsCoins().First();
			// Spend the received coin
			tx = CreateSpendingTransaction(createdCoin, keys[1].PubKey.WitHash.ScriptPubKey);
			transactionProcessor.Process(tx);

			// Spend the same coin again
			tx = CreateSpendingTransaction(createdCoin, keys[2].PubKey.WitHash.ScriptPubKey);
			var relevant = transactionProcessor.Process(tx);

			Assert.False(relevant);
			Assert.Single(transactionProcessor.Coins, coin => coin.Unspent);
			Assert.Single(transactionProcessor.Coins, coin => !coin.Unspent);
			Assert.Equal(2, transactionProcessor.TransactionCache.Count());
			Assert.Empty(transactionProcessor.TransactionHashes);
		}

		[Fact]
		public void ConfirmedDoubleSpend()
		{
			var transactionProcessor = CreateTransactionProcessor();

			var keys = transactionProcessor.KeyManager.GetKeys().ToArray();

			// An unconfirmed segwit transaction for us
			var tx = CreateCreditingTransaction(keys[0].PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m), isConfirmed: true);
			transactionProcessor.Process(tx);

			var createdCoin = tx.Transaction.Outputs.AsCoins().First();
			// Spend the received coin
			tx = CreateSpendingTransaction(createdCoin, keys[1].PubKey.WitHash.ScriptPubKey);
			transactionProcessor.Process(tx);

			// Spend it coin
			tx = CreateSpendingTransaction(createdCoin, keys[2].PubKey.WitHash.ScriptPubKey, isConfirmed: true);
			var relevant = transactionProcessor.Process(tx);

			Assert.True(relevant);
			Assert.Single(transactionProcessor.Coins, coin => coin.Unspent && coin.Confirmed);
			Assert.Single(transactionProcessor.Coins, coin => !coin.Unspent && coin.Confirmed);
			Assert.Empty(transactionProcessor.TransactionHashes);
		}

		[Fact]
		public void HandlesRBF()
		{
			var transactionProcessor = CreateTransactionProcessor();

			var keys = transactionProcessor.KeyManager.GetKeys().ToArray();

			// A confirmed segwit transaction for us
			var tx = CreateCreditingTransaction(keys[0].PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m), isConfirmed: true);
			transactionProcessor.Process(tx);

			var createdCoin = tx.Transaction.Outputs.AsCoins().First();
			// Spend the received coin
			tx = CreateSpendingTransaction(createdCoin, keys[1].PubKey.WitHash.ScriptPubKey);
			tx.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;
			var relevant = transactionProcessor.Process(tx);
			Assert.True(relevant);

			// Spend it coin
			tx = CreateSpendingTransaction(createdCoin, keys[2].PubKey.WitHash.ScriptPubKey);
			tx.Transaction.Outputs[0].Value = Money.Coins(0.9m);
			relevant = transactionProcessor.Process(tx);

			Assert.True(relevant);
			Assert.Single(transactionProcessor.Coins, coin => coin.Unspent && coin.Amount == Money.Coins(0.9m) && coin.IsReplaceable);
			Assert.Single(transactionProcessor.Coins, coin => !coin.Unspent && coin.Amount == Money.Coins(1.0m) && !coin.IsReplaceable);
			Assert.Empty(transactionProcessor.TransactionHashes);
		}

		[Fact]
		public void ReceiveTransactionForWallet()
		{
			var transactionProcessor = CreateTransactionProcessor();
			SmartCoin receivedCoin = null;
			transactionProcessor.CoinReceived += (s, theCoin) => receivedCoin = theCoin;
			var keys = transactionProcessor.KeyManager.GetKeys();
			var tx = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(1.0m));

			var relevant = transactionProcessor.Process(tx);

			// It is relevant because is funding the wallet
			Assert.True(relevant);
			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal(Money.Coins(1.0m), coin.Amount);
			Assert.Contains(transactionProcessor.TransactionCache, x => x == tx);
			Assert.NotNull(receivedCoin);
		}

		[Fact]
		public void SpendCoin()
		{
			var transactionProcessor = CreateTransactionProcessor();
			SmartCoin spentCoin = null;
			transactionProcessor.CoinSpent += (s, theCoin) => spentCoin = theCoin;
			var keys = transactionProcessor.KeyManager.GetKeys();
			var tx = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx);

			var createdCoin = tx.Transaction.Outputs.AsCoins().First();
			// Spend the received coin
			tx = CreateSpendingTransaction(createdCoin, new Key().PubKey.ScriptPubKey);
			var relevant = transactionProcessor.Process(tx);

			Assert.True(relevant);
			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.False(coin.Unspent);
			Assert.NotNull(spentCoin);
			Assert.Equal(coin, spentCoin);
		}

		[Fact]
		public void ReceiveTransactionWithDustForWallet()
		{
			var transactionProcessor = CreateTransactionProcessor();
			transactionProcessor.CoinReceived += (s, theCoin)
				=> throw new Exception("The dust coin raised an event when it shouldn't.");
			var keys = transactionProcessor.KeyManager.GetKeys();
			var tx = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(0.000099m));

			var relevant = transactionProcessor.Process(tx);

			// It is relevant even when all the coins can be dust.
			Assert.True(relevant);
			Assert.Empty(transactionProcessor.Coins);
		}

		[Fact]
		public void ReceiveCoinJoinTransaction()
		{
			var transactionProcessor = CreateTransactionProcessor();
			var keys = transactionProcessor.KeyManager.GetKeys();

			var amount = Money.Coins(0.1m);

			var tx = Network.RegTest.CreateTransaction();
			tx.Version = 1;
			tx.LockTime = LockTime.Zero;
			tx.Outputs.Add(amount, keys.First().P2wpkhScript);
			var txOut = new TxOut(amount, new Key().PubKey.WitHash.ScriptPubKey);
			tx.Outputs.AddRange(Enumerable.Repeat(txOut, 5)); // 6 indistinguishable txouts
			tx.Inputs.AddRange(Enumerable.Repeat(new TxIn(GetRandomOutPoint(), Script.Empty), 4));

			var relevant = transactionProcessor.Process(new SmartTransaction(tx, Height.Mempool));

			// It is relevant even when all the coins can be dust.
			Assert.True(relevant);
			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal(4, coin.AnonymitySet);
			Assert.Equal(amount, coin.Amount);
			Assert.False(coin.IsLikelyCoinJoinOutput);  // It is a coinjoin however we are reveiving but not spending.
		}

		[Fact]
		public void ReceiveWasabiCoinJoinTransaction()
		{
			var transactionProcessor = CreateTransactionProcessor();
			var keys = transactionProcessor.KeyManager.GetKeys();
			var amount = Money.Coins(0.1m);

			var stx = CreateCreditingTransaction(keys.First().P2wpkhScript, amount);
			transactionProcessor.Process(stx);

			var createdCoin = stx.Transaction.Outputs.AsCoins().First();

			var tx = Network.RegTest.CreateTransaction();
			tx.Version = 1;
			tx.LockTime = LockTime.Zero;
			tx.Outputs.Add(amount, keys.First().P2wpkhScript);
			var txOut = new TxOut(Money.Coins(0.1m), new Key().PubKey.WitHash.ScriptPubKey);
			tx.Outputs.AddRange(Enumerable.Repeat(txOut, 5)); // 6 indistinguishable txouts
			tx.Inputs.Add(createdCoin.Outpoint, Script.Empty, WitScript.Empty);
			tx.Inputs.AddRange(Enumerable.Repeat(new TxIn(GetRandomOutPoint(), Script.Empty), 4));

			var relevant = transactionProcessor.Process(new SmartTransaction(tx, Height.Mempool));

			// It is relevant even when all the coins can be dust.
			Assert.True(relevant);
			var coin = Assert.Single(transactionProcessor.Coins, c => c.AnonymitySet > 1);
			Assert.Equal(5, coin.AnonymitySet);
			Assert.Equal(amount, coin.Amount);
			Assert.True(coin.IsLikelyCoinJoinOutput);  // because we are spanding and receiving almost the same amount
		}

		private static TransactionProcessor CreateTransactionProcessor()
		{
			var keyManager = KeyManager.CreateNew(out _, "password");
			keyManager.AssertCleanKeysIndexed();
			return new TransactionProcessor(
				keyManager,
				new ConcurrentHashSet<uint256>(),
				new ObservableConcurrentHashSet<SmartCoin>(),
				Money.Coins(0.0001m),
				new ConcurrentHashSet<SmartTransaction>());
		}

		private static SmartTransaction CreateSpendingTransaction(Coin coin, Script scriptPubKey = null, bool isConfirmed = false)
		{
			var tx = Network.RegTest.CreateTransaction();
			tx.Inputs.Add(coin.Outpoint, Script.Empty, WitScript.Empty);
			tx.Outputs.Add(coin.Amount, scriptPubKey ?? Script.Empty);
			return new SmartTransaction(tx, isConfirmed ? new Height(9999) : Height.Mempool);
		}

		private static SmartTransaction CreateCreditingTransaction(Script scriptPubKey, Money amount, bool isConfirmed = false)
		{
			var tx = Network.RegTest.CreateTransaction();
			tx.Version = 1;
			tx.LockTime = LockTime.Zero;
			tx.Inputs.Add(GetRandomOutPoint(), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
			tx.Outputs.Add(amount, scriptPubKey);
			return new SmartTransaction(tx, isConfirmed ? new Height(9999) : Height.Mempool);
		}

		private static OutPoint GetRandomOutPoint()
		{
			return new OutPoint(RandomUtils.GetUInt256(), 0);
		}
	}
}
