using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class TransactionProcessorTests
	{
		[Fact]
		public async Task TransactionDoesNotCointainCoinsForTheWalletAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			// This transaction doesn't have any coin for the wallet. It is not relevant.
			var tx = CreateCreditingTransaction(new Key().PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m));

			var relevant = transactionProcessor.Process(tx);

			Assert.False(relevant.IsNews);
			Assert.Empty(transactionProcessor.Coins);
			Assert.True(transactionProcessor.TransactionStore.MempoolStore.IsEmpty());
			Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
		}

		[Fact]
		public async Task SpendToLegacyScriptsAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();
			var keys = transactionProcessor.KeyManager.GetKeys().ToArray();

			// A payment to a key under our control but using P2PKH script (legacy)
			var tx = CreateCreditingTransaction(keys.First().P2pkhScript, Money.Coins(1.0m));
			var relevant = transactionProcessor.Process(tx);

			Assert.False(relevant.IsNews);
			Assert.Empty(transactionProcessor.Coins);
			Assert.True(transactionProcessor.TransactionStore.MempoolStore.IsEmpty());
			Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
		}

		[Fact]
		public async Task UnconfirmedTransactionIsNotSegWitAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			// No segwit transaction. Ignore it.
			var tx = CreateCreditingTransaction(new Key().PubKey.Hash.ScriptPubKey, Money.Coins(1.0m));

			var relevant = transactionProcessor.Process(tx);

			Assert.False(relevant.IsNews);
			Assert.Empty(transactionProcessor.Coins);
			Assert.True(transactionProcessor.TransactionStore.MempoolStore.IsEmpty());
			Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
		}

		[Fact]
		public async Task ConfirmedTransactionIsNotSegWitAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			// No segwit transaction. Ignore it.
			var tx = CreateCreditingTransaction(new Key().PubKey.Hash.ScriptPubKey, Money.Coins(1.0m), height: 54321);

			var relevant = transactionProcessor.Process(tx);

			Assert.False(relevant.IsNews);
			Assert.Empty(transactionProcessor.Coins);
			Assert.True(transactionProcessor.TransactionStore.MempoolStore.IsEmpty());
			Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
		}

		[Fact]
		public async Task ProcessResultAfterConfirmationCorrectAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			// An unconfirmed segwit transaction for us
			var key = transactionProcessor.KeyManager.GetKeys().First();

			var tx1 = CreateCreditingTransaction(key.PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m));
			var res = transactionProcessor.Process(tx1);
			Assert.False(res.IsLikelyOwnCoinJoin);
			Assert.Empty(res.NewlyConfirmedReceivedCoins);
			Assert.Empty(res.NewlyConfirmedSpentCoins);
			Assert.Single(res.NewlyReceivedCoins);
			Assert.Empty(res.NewlySpentCoins);
			Assert.Single(res.ReceivedCoins);
			Assert.Empty(res.SpentCoins);
			Assert.Empty(res.ReceivedDusts);
			Assert.Empty(res.ReplacedCoins);
			Assert.Empty(res.RestoredCoins);
			Assert.Empty(res.SuccessfullyDoubleSpentCoins);
			Assert.True(res.IsNews);
			Assert.NotNull(res.Transaction);

			var blockHeight = new Height(77551);
			tx1 = new SmartTransaction(tx1.Transaction, blockHeight);
			res = transactionProcessor.Process(tx1);
			Assert.False(res.IsLikelyOwnCoinJoin);
			Assert.Single(res.NewlyConfirmedReceivedCoins);
			Assert.Empty(res.NewlyConfirmedSpentCoins);
			Assert.Empty(res.NewlyReceivedCoins);
			Assert.Empty(res.NewlySpentCoins);
			Assert.Single(res.ReceivedCoins);
			Assert.Empty(res.SpentCoins);
			Assert.Empty(res.ReceivedDusts);
			Assert.Empty(res.ReplacedCoins);
			Assert.Empty(res.RestoredCoins);
			Assert.Empty(res.SuccessfullyDoubleSpentCoins);
			Assert.True(res.IsNews);
			Assert.NotNull(res.Transaction);

			var tx2 = CreateSpendingTransaction(tx1.Transaction.Outputs.AsCoins().First(), new Key().PubKey.WitHash.ScriptPubKey);
			res = transactionProcessor.Process(tx2);
			Assert.False(res.IsLikelyOwnCoinJoin);
			Assert.Empty(res.NewlyConfirmedReceivedCoins);
			Assert.Empty(res.NewlyConfirmedSpentCoins);
			Assert.Empty(res.NewlyReceivedCoins);
			Assert.Single(res.NewlySpentCoins);
			Assert.Empty(res.ReceivedCoins);
			Assert.Single(res.SpentCoins);
			Assert.Empty(res.ReceivedDusts);
			Assert.Empty(res.ReplacedCoins);
			Assert.Empty(res.RestoredCoins);
			Assert.Empty(res.SuccessfullyDoubleSpentCoins);
			Assert.True(res.IsNews);
			Assert.NotNull(res.Transaction);

			blockHeight = new Height(77552);
			tx2 = new SmartTransaction(tx2.Transaction, blockHeight);
			res = transactionProcessor.Process(tx2);
			Assert.False(res.IsLikelyOwnCoinJoin);
			Assert.Empty(res.NewlyConfirmedReceivedCoins);
			Assert.Single(res.NewlyConfirmedSpentCoins);
			Assert.Empty(res.NewlyReceivedCoins);
			Assert.Empty(res.NewlySpentCoins);
			Assert.Empty(res.ReceivedCoins);
			Assert.Single(res.SpentCoins);
			Assert.Empty(res.ReceivedDusts);
			Assert.Empty(res.ReplacedCoins);
			Assert.Empty(res.RestoredCoins);
			Assert.Empty(res.SuccessfullyDoubleSpentCoins);
			Assert.True(res.IsNews);
			Assert.NotNull(res.Transaction);
		}

		[Fact]
		public async Task UpdateTransactionHeightAfterConfirmationAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			// An unconfirmed segwit transaction for us
			var key = transactionProcessor.KeyManager.GetKeys().First();

			var tx = CreateCreditingTransaction(key.PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m));
			transactionProcessor.Process(tx);

			Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
			var cachedTx = Assert.Single(transactionProcessor.TransactionStore.MempoolStore.GetTransactions());
			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal(Height.Mempool, cachedTx.Height);
			Assert.Equal(Height.Mempool, coin.Height);

			// Now it is confirmed
			var blockHeight = new Height(77551);
			tx = new SmartTransaction(tx.Transaction, blockHeight);
			var relevant = transactionProcessor.Process(tx);

			Assert.True(relevant.IsNews);
			Assert.Single(transactionProcessor.Coins);

			// Transaction store assertions
			Assert.True(transactionProcessor.TransactionStore.MempoolStore.IsEmpty());
			cachedTx = Assert.Single(transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions());
			Assert.Equal(blockHeight, cachedTx.Height);
			coin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal(blockHeight, coin.Height);
			Assert.True(coin.Confirmed);
		}

		[Fact]
		public async Task IgnoreDoubleSpendAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			var keys = transactionProcessor.KeyManager.GetKeys().ToArray();

			// An unconfirmed segwit transaction for us
			var tx0 = CreateCreditingTransaction(keys[0].PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m));
			transactionProcessor.Process(tx0);

			var createdCoin = tx0.Transaction.Outputs.AsCoins().First();
			// Spend the received coin
			var tx1 = CreateSpendingTransaction(createdCoin, keys[1].PubKey.WitHash.ScriptPubKey);
			transactionProcessor.Process(tx1);

			// Spend the same coin again
			var tx2 = CreateSpendingTransaction(createdCoin, keys[2].PubKey.WitHash.ScriptPubKey);
			var relevant = transactionProcessor.Process(tx2);

			Assert.False(relevant.IsNews);
			Assert.Single(transactionProcessor.Coins, coin => coin.Unspent);
			Assert.Single(transactionProcessor.Coins.AsAllCoinsView(), coin => !coin.Unspent);

			// Transaction store assertions
			Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
			var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
			Assert.Equal(2, mempool.Count());
			Assert.Equal(tx0, mempool.First());
			Assert.Equal(tx1, mempool.Last());
		}

		[Fact]
		public async Task ConfirmedDoubleSpendAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			var keys = transactionProcessor.KeyManager.GetKeys().ToArray();

			int doubleSpendReceived = 0;
			transactionProcessor.WalletRelevantTransactionProcessed += (s, e) =>
			{
				var doubleSpents = e.SuccessfullyDoubleSpentCoins;
				if (doubleSpents.Any())
				{
					var coin = Assert.Single(doubleSpents);
					// Double spend to ourselves but to a different address. So checking the address.
					Assert.Equal(keys[1].PubKey.WitHash.ScriptPubKey, coin.ScriptPubKey);

					doubleSpendReceived++;
				}
			};

			int coinReceivedCalled = 0;
			// The coin with the confirmed tx should win.
			transactionProcessor.WalletRelevantTransactionProcessed += (s, e) =>
			{
				foreach (var c in e.NewlyReceivedCoins)
				{
					switch (coinReceivedCalled)
					{
						case 0: Assert.Equal(keys[0].PubKey.WitHash.ScriptPubKey, c.ScriptPubKey); break;
						case 1: Assert.Equal(keys[1].PubKey.WitHash.ScriptPubKey, c.ScriptPubKey); break;
						case 2: Assert.Equal(keys[2].PubKey.WitHash.ScriptPubKey, c.ScriptPubKey); break;
						default:
							throw new InvalidOperationException();
					};

					coinReceivedCalled++;
				}
			};

			// An unconfirmed segwit transaction for us
			var tx0 = CreateCreditingTransaction(keys[0].PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx0);

			var createdCoin = tx0.Transaction.Outputs.AsCoins().First();
			// Spend the received coin
			var tx1 = CreateSpendingTransaction(createdCoin, keys[1].PubKey.WitHash.ScriptPubKey);
			transactionProcessor.Process(tx1);

			Assert.Equal(0, doubleSpendReceived);
			// Spend it coin
			var tx2 = CreateSpendingTransaction(createdCoin, keys[2].PubKey.WitHash.ScriptPubKey, height: 54321);
			var relevant = transactionProcessor.Process(tx2);
			Assert.Equal(1, doubleSpendReceived);

			Assert.True(relevant.IsNews);
			Assert.Single(transactionProcessor.Coins, coin => coin.Unspent && coin.Confirmed);
			Assert.Single(transactionProcessor.Coins.AsAllCoinsView(), coin => !coin.Unspent && coin.Confirmed);

			// Transaction store assertions
			var matureTxs = transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions();
			Assert.Equal(tx0, matureTxs.First());
			Assert.Equal(tx2, matureTxs.Last());

			// Unconfirmed transaction must be removed from the mempool because there is confirmed tx now
			var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
			Assert.Empty(mempool);
		}

		[Fact]
		public async Task HandlesRbfAsync()
		{
			// --tx0---> (A) --tx1 (replaceable)-+--> (B) --tx2---> (D)
			//                  |                |
			//                  |                +--> (C)
			//                  |
			//                  +--tx3 (replacement)---> (E)
			//
			var transactionProcessor = await CreateTransactionProcessorAsync();

			int replaceTransactionReceivedCalled = 0;
			transactionProcessor.WalletRelevantTransactionProcessed += (s, e) =>
			{
				if (e.ReplacedCoins.Any() || e.RestoredCoins.Any())
				{
					// Move the original coin from spent to unspent - so add.
					var originalCoin = Assert.Single(e.RestoredCoins);
					Assert.Equal(Money.Coins(1.0m), originalCoin.Amount);

					// Remove the created coin by the transaction.
					Assert.Equal(3, e.ReplacedCoins.Count());
					Assert.Single(e.ReplacedCoins, coin => coin.HdPubKey.Label == "B");
					Assert.Single(e.ReplacedCoins, coin => coin.HdPubKey.Label == "C");
					Assert.Single(e.ReplacedCoins, coin => coin.HdPubKey.Label == "D");

					replaceTransactionReceivedCalled++;
				}
			};

			// A confirmed segwit transaction for us
			var tx0 = CreateCreditingTransaction(transactionProcessor.NewKey("A").P2wpkhScript, Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx0);

			var createdCoin = tx0.Transaction.Outputs.AsCoins().First();
			// Spend the received coin
			var tx1 = CreateSpendingTransaction(createdCoin, transactionProcessor.NewKey("B").P2wpkhScript);
			tx1.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;
			tx1.Transaction.Outputs[0].Value = Money.Coins(0.95m);
			tx1.Transaction.Outputs.Add(Money.Coins(0.1m), transactionProcessor.NewKey("C").P2wpkhScript);
			var relevant = transactionProcessor.Process(tx1);
			Assert.True(relevant.IsNews);
			Assert.Equal(0, replaceTransactionReceivedCalled);

			var unconfirmedCoin1 = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Label == "B");
			var unconfirmedCoin2 = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Label == "C");
			Assert.True(unconfirmedCoin1.IsReplaceable);
			Assert.True(unconfirmedCoin2.IsReplaceable);

			// Spend the received coin
			var tx2 = CreateSpendingTransaction(unconfirmedCoin1.GetCoin(), transactionProcessor.NewKey("D").P2wpkhScript);
			tx2.Transaction.Outputs[0].Value = Money.Coins(0.7m);
			relevant = transactionProcessor.Process(tx2);
			Assert.True(relevant.IsNews);
			Assert.Equal(0, replaceTransactionReceivedCalled);

			// Spend it coin
			var tx3 = CreateSpendingTransaction(createdCoin, transactionProcessor.NewKey("E").P2wpkhScript);
			tx3.Transaction.Outputs[0].Value = Money.Coins(0.9m);
			relevant = transactionProcessor.Process(tx3);

			Assert.True(relevant.IsNews);
			Assert.Equal(1, replaceTransactionReceivedCalled);
			var finalCoin = Assert.Single(transactionProcessor.Coins);
			Assert.True(finalCoin.IsReplaceable);
			Assert.Equal("E", finalCoin.HdPubKey.Label);

			Assert.DoesNotContain(unconfirmedCoin1, transactionProcessor.Coins.AsAllCoinsView());

			// Transaction store assertions
			var matureTxs = transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions();
			Assert.Equal(tx0, matureTxs.First());

			// All the replaced transactions tx1 and tx2 have to be removed because tx4 replaced tx1
			var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
			var txInMempool = Assert.Single(mempool);
			Assert.Equal(tx3, txInMempool);
		}

		[Fact]
		public async Task ConfirmTransactionTestAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			var keys = transactionProcessor.KeyManager.GetKeys().ToArray();
			int confirmed = 0;
			transactionProcessor.WalletRelevantTransactionProcessed += (s, e) =>
			{
				if (e.NewlySpentCoins.Any())
				{
					throw new InvalidOperationException("We are not spending the coin.");
				}
				else if (e.NewlyConfirmedSpentCoins.Any())
				{
					confirmed++;
				}
			};

			// A confirmed segwit transaction for us
			var tx1 = CreateCreditingTransaction(keys[0].PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m));
			var res = transactionProcessor.Process(tx1);
			Assert.True(res.IsNews);
			Assert.Single(res.NewlyReceivedCoins);
			Assert.Single(res.ReceivedCoins);
			Assert.Empty(res.NewlyConfirmedReceivedCoins);
			Assert.Empty(res.ReceivedDusts);

			// Process it again.
			res = transactionProcessor.Process(tx1);
			Assert.False(res.IsNews);
			Assert.Empty(res.ReplacedCoins);
			Assert.Empty(res.RestoredCoins);
			Assert.Empty(res.SuccessfullyDoubleSpentCoins);
			Assert.Single(res.ReceivedCoins);
			Assert.Empty(res.NewlyConfirmedReceivedCoins);
			Assert.Empty(res.ReceivedDusts);

			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.False(coin.Confirmed);

			var tx2 = new SmartTransaction(tx1.Transaction.Clone(), new Height(54321));

			Assert.Equal(tx1.GetHash(), tx2.GetHash());
			res = transactionProcessor.Process(tx2);
			Assert.True(res.IsNews);
			Assert.Empty(res.ReplacedCoins);
			Assert.Empty(res.RestoredCoins);
			Assert.Empty(res.SuccessfullyDoubleSpentCoins);
			Assert.Single(res.ReceivedCoins);
			Assert.Single(res.NewlyConfirmedReceivedCoins);
			Assert.Empty(res.ReceivedDusts);
			Assert.True(coin.Confirmed);

			Assert.Equal(0, confirmed);

			// Transaction store assertions
			var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
			Assert.Empty(mempool);

			var matureTxs = transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions();
			var confirmedTx = Assert.Single(matureTxs);
			Assert.Equal(tx1, confirmedTx);
			Assert.Equal(tx2, confirmedTx);
		}

		[Fact]
		public async Task HandlesBumpFeeAsync()
		{
			// --tx0---> (A) --+
			//                 +--+-- tx2 (replaceable --+---> (myself)
			// --tx1---> (B) --+  |                      |
			//                    |                      +---> (change myself)
			//                    |
			//                    +--- tx4 (replaces tx2)
			//                    |
			// --tx3---> (C) -----+

			// Replaces a previous RBF transaction by a new one that contains one more input (higher fee)
			var transactionProcessor = await CreateTransactionProcessorAsync();
			Script NewScript(string label) => transactionProcessor.NewKey(label).P2wpkhScript;

			// A confirmed segwit transaction for us
			var tx0 = CreateCreditingTransaction(NewScript("A"), Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx0);

			// Another confirmed segwit transaction for us
			var tx1 = CreateCreditingTransaction(NewScript("B"), Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx1);

			var createdCoins = transactionProcessor.Coins.Select(x => x.GetCoin()).ToArray(); // 2 coins of 1.0 btc each

			// Spend the received coins
			var destinationScript = NewScript("myself");
			var changeScript = NewScript("Change myself");
			var tx2 = CreateSpendingTransaction(createdCoins, destinationScript, changeScript); // spends 1.2btc
			tx2.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;
			var relevant = transactionProcessor.Process(tx2);
			Assert.True(relevant.IsNews);

			// Another confirmed segwit transaction for us
			var tx3 = CreateCreditingTransaction(NewScript("C"), Money.Coins(1.0m), height: 54322);
			transactionProcessor.Process(tx3);

			// At this moment we have one 1.2btc and one 0.8btc replaceable coins and one 1.0btc final coin
			var latestCreatedCoin = Assert.Single(transactionProcessor.Coins.CreatedBy(tx3.Transaction.GetHash()));
			var coinsToSpend = createdCoins.Concat(new[] { latestCreatedCoin.GetCoin() }).ToArray();

			// Spend them again with a different amount
			destinationScript = new Key().PubKey.WitHash.ScriptPubKey;  // spend to someone else
			var tx4 = CreateSpendingTransaction(coinsToSpend, destinationScript, changeScript);
			relevant = transactionProcessor.Process(tx4);

			Assert.True(relevant.IsNews);
			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.True(coin.IsReplaceable);

			// Transaction store assertions
			var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
			var inMempoolTx = Assert.Single(mempool);
			Assert.Equal(tx4, inMempoolTx);

			var matureTxs = transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions().ToArray();
			Assert.Equal(3, matureTxs.Length);
			Assert.Equal(tx0, matureTxs[0]);
			Assert.Equal(tx1, matureTxs[1]);
			Assert.Equal(tx3, matureTxs[2]);
		}

		[Fact]
		public async Task HandlesRbfWithLessCoinsAsync()
		{
			// --tx0---> (A) --+--+
			//                 +--|-- tx2 (replaceable --+---> (myself)
			// --tx1---> (B) --+  |                      |
			//                    |                      +---> (change myself)
			//                    |
			//                    +--- tx3 (replaces tx2)

			// Replaces a previous RBF transaction by a new one that contains one less input (higher fee)
			var transactionProcessor = await CreateTransactionProcessorAsync();
			Script NewScript(string label) => transactionProcessor.NewKey(label).P2wpkhScript;

			// A confirmed segwit transaction for us
			var tx0 = CreateCreditingTransaction(NewScript("A"), Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx0);

			// Another confirmed segwit transaction for us
			var tx1 = CreateCreditingTransaction(NewScript("B"), Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx1);

			var createdCoins = transactionProcessor.Coins.Select(x => x.GetCoin()).ToArray(); // 2 coins of 1.0 btc each

			// Spend the received coins (replaceable)
			var destinationScript = NewScript("myself");
			var changeScript = NewScript("Change myself");
			var tx2 = CreateSpendingTransaction(createdCoins, destinationScript, changeScript); // spends 1.2btc
			tx2.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;
			var relevant = transactionProcessor.Process(tx2);
			Assert.True(relevant.IsNews);

			// replace previous tx with another one spending only one coin
			destinationScript = new Key().PubKey.WitHash.ScriptPubKey;  // spend to someone else
			var tx3 = CreateSpendingTransaction(createdCoins.Take(1), destinationScript, changeScript); // spends 0.6btc
			relevant = transactionProcessor.Process(tx3);

			Assert.True(relevant.IsNews);
			var replaceableCoin = Assert.Single(transactionProcessor.Coins, c => c.IsReplaceable);
			Assert.Equal(tx3.Transaction.GetHash(), replaceableCoin.TransactionId);

			var nonReplaceableCoin = Assert.Single(transactionProcessor.Coins, c => !c.IsReplaceable);
			Assert.Equal(tx1.Transaction.GetHash(), nonReplaceableCoin.TransactionId);

			// Transaction store assertions
			var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
			var inMempoolTx = Assert.Single(mempool);
			Assert.Equal(tx3, inMempoolTx);

			var matureTxs = transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions().ToArray();
			Assert.Equal(2, matureTxs.Length);
			Assert.Equal(tx0, matureTxs[0]);
			Assert.Equal(tx1, matureTxs[1]);
		}

		[Fact]
		public async Task ReceiveTransactionForWalletAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();
			SmartCoin receivedCoin = null;
			transactionProcessor.WalletRelevantTransactionProcessed += (s, e) => receivedCoin = e.NewlyReceivedCoins.Single();
			var keys = transactionProcessor.KeyManager.GetKeys();
			var tx = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(1.0m));

			var relevant = transactionProcessor.Process(tx);

			// It is relevant because is funding the wallet
			Assert.True(relevant.IsNews);
			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal(Money.Coins(1.0m), coin.Amount);
			Assert.NotNull(receivedCoin);

			// Transaction store assertions
			var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
			var inMempoolTx = Assert.Single(mempool);
			Assert.Equal(tx, inMempoolTx);

			var matureTxs = transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions().ToArray();
			Assert.Empty(matureTxs);
		}

		[Fact]
		public async Task SpendCoinAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();
			SmartCoin spentCoin = null;
			transactionProcessor.WalletRelevantTransactionProcessed += (s, e) =>
			{
				if (e.NewlySpentCoins.Any())
				{
					spentCoin = e.NewlySpentCoins.Single();
				}
			};
			var keys = transactionProcessor.KeyManager.GetKeys();
			var tx0 = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx0);

			var createdCoin = tx0.Transaction.Outputs.AsCoins().First();
			// Spend the received coin
			var tx1 = CreateSpendingTransaction(createdCoin, new Key().PubKey.ScriptPubKey);
			var relevant = transactionProcessor.Process(tx1);

			Assert.True(relevant.IsNews);
			var coin = Assert.Single(transactionProcessor.Coins.AsAllCoinsView());
			Assert.False(coin.Unspent);
			Assert.NotNull(spentCoin);
			Assert.Equal(coin, spentCoin);

			// Transaction store assertions
			var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
			Assert.Equal(2, mempool.Count());

			var matureTxs = transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions().ToArray();
			Assert.Empty(matureTxs);
		}

		[Fact]
		public async Task ReceiveTransactionWithDustForWalletAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();
			transactionProcessor.WalletRelevantTransactionProcessed += (s, e) =>
			{
				// The dust coin should raise an event, but it shouldn't be fully processed.
				Assert.NotEmpty(e.ReceivedDusts);
				Assert.Empty(e.ReceivedCoins);
				Assert.Empty(e.NewlyReceivedCoins);
				Assert.Empty(e.NewlyConfirmedReceivedCoins);
			};
			var keys = transactionProcessor.KeyManager.GetKeys();
			var tx = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(0.000099m));

			var relevant = transactionProcessor.Process(tx);

			// It is relevant even when all the coins can be dust.
			Assert.True(relevant.IsNews);
			Assert.Empty(transactionProcessor.Coins);

			// Transaction store assertions
			var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
			Assert.Single(mempool);  // it doesn't matter if it is a dust only tx, we save the tx anyway.

			var matureTxs = transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions().ToArray();
			Assert.Empty(matureTxs);
		}

		[Fact]
		public async Task ReceiveCoinJoinTransactionAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();
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
			Assert.True(relevant.IsNews);
			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal(4, coin.AnonymitySet);
			Assert.Equal(amount, coin.Amount);
			Assert.False(coin.IsLikelyCoinJoinOutput);  // It is a coinjoin however we are reveiving but not spending.
		}

		[Fact]
		public async Task ReceiveWasabiCoinJoinTransactionAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();
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
			Assert.True(relevant.IsNews);
			var coin = Assert.Single(transactionProcessor.Coins, c => c.AnonymitySet > 1);
			Assert.Equal(5, coin.AnonymitySet);
			Assert.Equal(amount, coin.Amount);
			Assert.True(coin.IsLikelyCoinJoinOutput);  // because we are spanding and receiving almost the same amount
		}

		[Fact]
		public async Task SimpleDirectClusteringAsync()
		{
			// --tx0---> (A) --tx1-+---> (pay to B)
			//                     |
			//                     +---> (change of B - cluster B, A)  --tx2-+---> (pay to myself C - cluster C, B, A)
			//                                                               |
			//                                                               +---> (change of C - cluster C, B, A)
			//
			var transactionProcessor = await CreateTransactionProcessorAsync();
			var key = transactionProcessor.NewKey("A");
			var tx0 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx0);

			var createdCoin = transactionProcessor.Coins.First();
			Assert.Equal("A", createdCoin.Observers.Labels);

			// Spend the received coin to someone else B
			var changeScript1 = transactionProcessor.NewKey("B").P2wpkhScript;
			var tx1 = CreateSpendingTransaction(new[] { createdCoin.GetCoin() }, new Key().ScriptPubKey, changeScript1);
			transactionProcessor.Process(tx1);
			createdCoin = transactionProcessor.Coins.First();
			Assert.Equal("A, B", createdCoin.Observers.Labels);

			// Spend the received coin to mylself else C
			var myselfScript = transactionProcessor.NewKey("C").P2wpkhScript;
			var changeScript2 = transactionProcessor.NewKey("").P2wpkhScript;
			var tx2 = CreateSpendingTransaction(new[] { createdCoin.GetCoin() }, myselfScript, changeScript2);
			transactionProcessor.Process(tx2);

			Assert.Equal(2, transactionProcessor.Coins.Count());
			createdCoin = transactionProcessor.Coins.First();
			Assert.Equal("A, B, C", createdCoin.Observers.Labels);

			var createdchangeCoin = transactionProcessor.Coins.Last();
			Assert.Equal("A, B, C", createdchangeCoin.Observers.Labels);
		}

		[Fact]
		public async Task MultipleDirectClusteringAsync()
		{
			// --tx0---> (A) --+
			//                 |
			// --tx1---> (B) --+---tx3-+--> (pay to D)
			//                 |       |
			// --tx2---> (C) --+       +--> (change of D - cluster D, A, B, C)---+
			//                                                                   |
			//                                                                   +---tx5--+-->(pay to F)
			//                                                                   |        |
			// --tx4---> (E) ----------------------------------------------------+        +-->(change of F - cluster F, D, A, B, C, E)
			var transactionProcessor = await CreateTransactionProcessorAsync();
			var key = transactionProcessor.NewKey("A");
			var tx0 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx0);

			key = transactionProcessor.NewKey("B");
			var tx1 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx1);

			key = transactionProcessor.NewKey("C");
			var tx2 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx2);

			var scoinA = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "A");
			var scoinB = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "B");
			var scoinC = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "C");

			var changeScript = transactionProcessor.NewKey("D").P2wpkhScript;
			var coins = new[] { scoinA.GetCoin(), scoinB.GetCoin(), scoinC.GetCoin() };
			var tx3 = CreateSpendingTransaction(coins, new Key().ScriptPubKey, changeScript);
			transactionProcessor.Process(tx3);

			var changeCoinD = Assert.Single(transactionProcessor.Coins);
			Assert.Equal("A, B, C, D", changeCoinD.Observers.Labels);

			key = transactionProcessor.NewKey("E");
			var tx4 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx4);
			var scoinE = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "E");

			changeScript = transactionProcessor.NewKey("F").P2wpkhScript;
			coins = new[] { changeCoinD.GetCoin(), scoinE.GetCoin() };
			var tx5 = CreateSpendingTransaction(coins, new Key().ScriptPubKey, changeScript);
			transactionProcessor.Process(tx5);

			var changeCoin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal("A, B, C, D, E, F", changeCoin.Observers.Labels);
		}

		[Fact]
		public async Task SameScriptClusteringAsync()
		{
			// --tx0---> (A) --+
			//                 |
			// --tx1---> (B) --+---tx3-+--> (pay to D - myself - cluster (D, A, B, C))
			//                 |
			// --tx2---> (C) --+
			//
			// --tx4---> (D - reuse - cluster (D, A, B, C))
			var transactionProcessor = await CreateTransactionProcessorAsync();
			var key = transactionProcessor.NewKey("A");
			var tx0 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx0);

			key = transactionProcessor.NewKey("B");
			var tx1 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx1);

			key = transactionProcessor.NewKey("C");
			var tx2 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx2);

			var scoinA = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "A");
			var scoinB = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "B");
			var scoinC = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "C");

			var myself = transactionProcessor.NewKey("D").P2wpkhScript;
			var changeScript = transactionProcessor.NewKey("").P2wpkhScript;
			var coins = new[] { scoinA.GetCoin(), scoinB.GetCoin(), scoinC.GetCoin() };
			var tx3 = CreateSpendingTransaction(coins, myself, changeScript);
			transactionProcessor.Process(tx3);

			var paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
			Assert.Equal("A, B, C, D", paymentCoin.Observers.Labels);

			var tx4 = CreateCreditingTransaction(myself, Money.Coins(7.0m));
			transactionProcessor.Process(tx4);
			Assert.Equal(2, transactionProcessor.Coins.Count(c => c.ScriptPubKey == myself));
			var newPaymentCoin = Assert.Single(transactionProcessor.Coins, c => c.Amount == Money.Coins(7.0m));
			Assert.Equal("A, B, C, D", newPaymentCoin.Observers.Labels);
		}


		[Fact]
		public async Task SameScriptClusterAfterSpendingAsync()
		{
			// If there are two coins with the same scriptPubKey after we spend one of them,
			// both have to share the same cluster. 
			//
			// --tx0---> (A) -->
			//                 
			// --tx1---> (A) ---tx2---> (B - cluster (B, A))
			var transactionProcessor = await CreateTransactionProcessorAsync();
			var key = transactionProcessor.NewKey("A");
			var tx0 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx0);

			var tx1 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			var result = transactionProcessor.Process(tx1);

			key = transactionProcessor.NewKey("B");
			var tx2 = CreateSpendingTransaction(result.ReceivedCoins[0].GetCoin(), key.P2wpkhScript);
			transactionProcessor.Process(tx2);

			var coins = transactionProcessor.Coins;
			Assert.Equal(coins.First().Observers, coins.Last().Observers);
			Assert.Equal("A, B", coins.First().Observers.Labels.ToString());
		}


		[Fact]
		public async Task SameClusterAfterReplacedByFeeAsync()
		{
			// --tx0---> (A) --+
			//                 |
			// --tx1---> (B) --+---tx3 (replaceable)---> (pay to D - myself - cluster (D, A, B, C))
			//                 |    |
			// --tx2---> (C) --+    +----tx4 (replacement)---> (pay to D - coins is different order - cluster (D, A, B, C))
			//
			var transactionProcessor = await CreateTransactionProcessorAsync();
			var key = transactionProcessor.NewKey("A");
			var tx0 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx0);

			key = transactionProcessor.NewKey("B");
			var tx1 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx1);

			key = transactionProcessor.NewKey("C");
			var tx2 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx2);

			var scoinA = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "A");
			var scoinB = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "B");
			var scoinC = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "C");

			var myself = transactionProcessor.NewKey("D").P2wpkhScript;
			var changeScript = transactionProcessor.NewKey("").P2wpkhScript;
			var coins = new[] { scoinA.GetCoin(), scoinB.GetCoin(), scoinC.GetCoin() };
			var tx3 = CreateSpendingTransaction(coins, myself, changeScript, replaceable: true);
			transactionProcessor.Process(tx3);

			var paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
			Assert.Equal("A, B, C, D", paymentCoin.Observers.Labels);

			coins = new[] { scoinB.GetCoin(), scoinC.GetCoin(), scoinA.GetCoin() };
			var tx4 = CreateSpendingTransaction(coins, myself, changeScript);
			transactionProcessor.Process(tx4);

			paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
			Assert.Equal("A, B, C, D", paymentCoin.Observers.Labels);
		}

		[Fact]
		public async Task UpdateClustersAfterReplacedByFeeWithNewCoinsAsync()
		{
			// --tx0---> (A) --+
			//                 |
			// --tx1---> (B) --+---tx3 (replaceable)---> (pay to D - myself - cluster (D, A, B, C))
			//                 |    |
			// --tx2---> (C) --+    +----tx5 (replacement)---> (pay to D - coins is different order - cluster (D, A, B, C, X))
			//                      |
			// --tx4---> (X) -------+
			var transactionProcessor = await CreateTransactionProcessorAsync();
			var key = transactionProcessor.NewKey("A");
			var tx0 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx0);

			key = transactionProcessor.NewKey("B");
			var tx1 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx1);

			key = transactionProcessor.NewKey("C");
			var tx2 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx2);

			var scoinA = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "A");
			var scoinB = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "B");
			var scoinC = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "C");

			var myself = transactionProcessor.NewKey("D").P2wpkhScript;
			var changeScript = transactionProcessor.NewKey("").P2wpkhScript;
			var coins = new[] { scoinA.GetCoin(), scoinB.GetCoin(), scoinC.GetCoin() };
			var tx3 = CreateSpendingTransaction(coins, myself, changeScript, replaceable: true);
			transactionProcessor.Process(tx3);

			var paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
			Assert.Equal("A, B, C, D", paymentCoin.Observers.Labels);

			key = transactionProcessor.NewKey("X");
			var tx4 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx4);
			var scoinX = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "X");

			coins = new[] { scoinB.GetCoin(), scoinX.GetCoin(), scoinC.GetCoin(), scoinA.GetCoin() };
			var tx5 = CreateSpendingTransaction(coins, myself, changeScript);
			transactionProcessor.Process(tx5);

			paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
			Assert.Equal("A, B, C, D, X", paymentCoin.Observers.Labels);
		}

		[Fact]
		public async Task RememberClusteringAfterReorgAsync()
		{
			// --tx0---> (A) --+
			//                 |
			// --tx1---> (B) --+---tx3----> (pay to D)
			//                 |    ^
			// --tx2---> (C) --+    |
			//                      +---- The block is reorg and tx3 is removed
			//
			var transactionProcessor = await CreateTransactionProcessorAsync();
			var key = transactionProcessor.NewKey("A");
			var tx0 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx0);

			key = transactionProcessor.NewKey("B");
			var tx1 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m), height: 54322);
			transactionProcessor.Process(tx1);

			key = transactionProcessor.NewKey("C");
			var tx2 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m), height: 54323);
			transactionProcessor.Process(tx2);

			var scoinA = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "A");
			var scoinB = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "B");
			var scoinC = Assert.Single(transactionProcessor.Coins, c => c.Observers.Labels == "C");

			var changeScript = transactionProcessor.NewKey("D").P2wpkhScript;
			var coins = new[] { scoinA.GetCoin(), scoinB.GetCoin(), scoinC.GetCoin() };
			var tx3 = CreateSpendingTransaction(coins, new Key().ScriptPubKey, changeScript, height: 55555);
			transactionProcessor.Process(tx3);

			var changeCoinD = Assert.Single(transactionProcessor.Coins);
			Assert.Equal("A, B, C, D", changeCoinD.Observers.Labels);
			Assert.Equal(scoinA.Observers, changeCoinD.Observers);
			Assert.Equal(scoinB.Observers, changeCoinD.Observers);
			Assert.Equal(scoinC.Observers, changeCoinD.Observers);

			// reorg
			Assert.True(changeCoinD.Confirmed);
			transactionProcessor.UndoBlock(tx3.Height);
			Assert.False(changeCoinD.Confirmed);

			Assert.Equal("A, B, C, D", changeCoinD.Observers.Labels);
			Assert.Equal(scoinA.Observers, changeCoinD.Observers);
			Assert.Equal(scoinB.Observers, changeCoinD.Observers);
			Assert.Equal(scoinC.Observers, changeCoinD.Observers);
		}

		[Fact]
		public async Task EnoughAnonymitySetClusteringAsync()
		{
			// --tx0---> (A) --tx1---> (empty)
			//
			// Note: tx1 is a coinjoin transaction

			var transactionProcessor = await CreateTransactionProcessorAsync();
			var key = transactionProcessor.NewKey("A");
			var tx0 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx0);

			var receivedCoin = Assert.Single(transactionProcessor.Coins);

			// build coinjoin transaction
			var cjtx = Network.RegTest.CreateTransaction();

			for (var i = 0; i < 100; i++)
			{
				cjtx.Inputs.Add(GetRandomOutPoint(), Script.Empty, WitScript.Empty);
			}
			for (var i = 0; i < 100; i++)
			{
				cjtx.Outputs.Add(Money.Coins(0.1m), Script.Empty);
			}
			cjtx.Inputs.Add(receivedCoin.GetOutPoint(), Script.Empty, WitScript.Empty);
			cjtx.Outputs.Add(Money.Coins(0.1m), transactionProcessor.NewKey("").P2wpkhScript);
			cjtx.Outputs.Add(Money.Coins(0.9m), transactionProcessor.NewKey("").P2wpkhScript);
			var tx1 = new SmartTransaction(cjtx, Height.Mempool);

			transactionProcessor.Process(tx1);
			var anonymousCoin = Assert.Single(transactionProcessor.Coins, c => c.Amount == Money.Coins(0.1m));
			var changeCoin = Assert.Single(transactionProcessor.Coins, c => c.Amount == Money.Coins(0.9m));

			Assert.Empty(anonymousCoin.Observers.Labels);
			Assert.NotEmpty(changeCoin.Observers.Labels);
		}

		private async Task<TransactionProcessor> CreateTransactionProcessorAsync([CallerFilePath]string callerFilePath = null, [CallerMemberName] string callerName = "")
		{
			var keyManager = KeyManager.CreateNew(out _, "password");
			keyManager.AssertCleanKeysIndexed();

			var txStore = new AllTransactionStore();
			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerName, "TransactionStore");
			await IoHelpers.DeleteRecursivelyWithMagicDustAsync(dir);
			await txStore.InitializeAsync(dir, Network.RegTest);

			return new TransactionProcessor(
				txStore,
				keyManager,
				Money.Coins(0.0001m));
		}

		private static SmartTransaction CreateSpendingTransaction(Coin coin, Script scriptPubKey = null, int height = 0)
		{
			var tx = Network.RegTest.CreateTransaction();
			tx.Inputs.Add(coin.Outpoint, Script.Empty, WitScript.Empty);
			tx.Outputs.Add(coin.Amount, scriptPubKey ?? Script.Empty);
			return new SmartTransaction(tx, height == 0 ? Height.Mempool : new Height(height));
		}

		private static SmartTransaction CreateSpendingTransaction(IEnumerable<Coin> coins, Script scriptPubKey, Script scriptPubKeyChange, bool replaceable = false, int height = 0)
		{
			var tx = Network.RegTest.CreateTransaction();
			var amount = Money.Zero;
			foreach (var coin in coins)
			{
				tx.Inputs.Add(coin.Outpoint, Script.Empty, WitScript.Empty, replaceable ? Sequence.MAX_BIP125_RBF_SEQUENCE : Sequence.SEQUENCE_FINAL);
				amount += coin.Amount;
			}
			tx.Outputs.Add(amount.Percentage(60), scriptPubKey ?? Script.Empty);
			tx.Outputs.Add(amount.Percentage(40), scriptPubKeyChange);
			return new SmartTransaction(tx, height == 0 ? Height.Mempool : new Height(height));
		}

		private static SmartTransaction CreateCreditingTransaction(Script scriptPubKey, Money amount, int height = 0)
		{
			var tx = Network.RegTest.CreateTransaction();
			tx.Version = 1;
			tx.LockTime = LockTime.Zero;
			tx.Inputs.Add(GetRandomOutPoint(), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
			tx.Inputs.Add(GetRandomOutPoint(), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
			tx.Outputs.Add(amount, scriptPubKey);
			return new SmartTransaction(tx, height == 0 ? Height.Mempool : new Height(height));
		}

		private static OutPoint GetRandomOutPoint()
		{
			return new OutPoint(RandomUtils.GetUInt256(), 0);
		}
	}

	internal static class TransactionProcessorExtensions
	{
		public static HdPubKey NewKey(this TransactionProcessor me, string label)
		{
			return me.KeyManager.GenerateNewKey(label, KeyState.Clean, true);
		}
	}
}
