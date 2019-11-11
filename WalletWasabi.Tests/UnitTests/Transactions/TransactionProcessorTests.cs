using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WalletWasabi.BlockchainAnalysis;
using WalletWasabi.Coins;
using WalletWasabi.KeyManagement;
using WalletWasabi.Models;
using WalletWasabi.Transactions;
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

			Assert.False(relevant);
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

			Assert.False(relevant);
			Assert.Empty(transactionProcessor.Coins);
		}

		[Fact]
		public async Task UnconfirmedTransactionIsNotSegWitAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			// No segwit transaction. Ignore it.
			var tx = CreateCreditingTransaction(new Key().PubKey.Hash.ScriptPubKey, Money.Coins(1.0m));

			var relevant = transactionProcessor.Process(tx);

			Assert.False(relevant);
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

			Assert.False(relevant);
			Assert.Empty(transactionProcessor.Coins);
			Assert.True(transactionProcessor.TransactionStore.MempoolStore.IsEmpty());
			Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
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

			Assert.True(relevant);
			Assert.Single(transactionProcessor.Coins);
			Assert.True(transactionProcessor.TransactionStore.MempoolStore.IsEmpty());
			cachedTx = Assert.Single(transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions());
			Assert.NotEqual(Height.Mempool, cachedTx.Height);
			coin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal(blockHeight, coin.Height);
		}

		[Fact]
		public async Task IgnoreDoubleSpendAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

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
			Assert.Single(transactionProcessor.Coins.AsAllCoinsView(), coin => !coin.Unspent);
			Assert.Equal(2, transactionProcessor.TransactionStore.GetTransactions().Count());
		}

		[Fact]
		public async Task ConfirmedDoubleSpendAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			var keys = transactionProcessor.KeyManager.GetKeys().ToArray();
			transactionProcessor.DoubleSpendReceived += (s, e) =>
			{
			};

			// An unconfirmed segwit transaction for us
			var tx = CreateCreditingTransaction(keys[0].PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx);

			var createdCoin = tx.Transaction.Outputs.AsCoins().First();
			// Spend the received coin
			tx = CreateSpendingTransaction(createdCoin, keys[1].PubKey.WitHash.ScriptPubKey);
			transactionProcessor.Process(tx);

			// Spend it coin
			tx = CreateSpendingTransaction(createdCoin, keys[2].PubKey.WitHash.ScriptPubKey, height: 54321);
			var relevant = transactionProcessor.Process(tx);

			Assert.True(relevant);
			Assert.Single(transactionProcessor.Coins, coin => coin.Unspent && coin.Confirmed);
			Assert.Single(transactionProcessor.Coins.AsAllCoinsView(), coin => !coin.Unspent && coin.Confirmed);
		}

		[Fact]
		public async Task HandlesRbfAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			var keys = transactionProcessor.KeyManager.GetKeys().ToArray();

			// A confirmed segwit transaction for us
			var tx = CreateCreditingTransaction(keys[0].PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m), height: 54321);
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
			Assert.Single(transactionProcessor.Coins, coin => coin.Amount == Money.Coins(0.9m) && coin.IsReplaceable);
			Assert.Single(transactionProcessor.Coins.AsAllCoinsView(), coin => !coin.Unspent && coin.Amount == Money.Coins(1.0m) && !coin.IsReplaceable);
		}

		[Fact]
		public async Task ConfirmTransactionTestAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();

			var keys = transactionProcessor.KeyManager.GetKeys().ToArray();
			int coinsReceived = 0;
			int confirmed = 0;
			transactionProcessor.CoinReceived += (s, e) => coinsReceived++;
			transactionProcessor.CoinSpent += (s, e) => throw new InvalidOperationException("We are not spending the coin.");
			transactionProcessor.SpenderConfirmed += (s, e) => confirmed++;

			// A confirmed segwit transaction for us
			var tx = CreateCreditingTransaction(keys[0].PubKey.WitHash.ScriptPubKey, Money.Coins(1.0m));
			transactionProcessor.Process(tx);
			Assert.Equal(1, coinsReceived);

			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.False(coin.Confirmed);

			var tx2 = new SmartTransaction(tx.Transaction.Clone(), new Height(54321));

			Assert.Equal(tx.GetHash(), tx2.GetHash());
			transactionProcessor.Process(tx2);
			Assert.Equal(1, coinsReceived);
			Assert.True(coin.Confirmed);

			Assert.Equal(0, confirmed);
		}

		[Fact]
		public async Task HandlesBumpFeeAsync()
		{
			// Replaces a previous RBF transaction by a new one that contains one more input (higher fee)
			var transactionProcessor = await CreateTransactionProcessorAsync();
			Script NewScript(string label) => transactionProcessor.NewKey(label).P2wpkhScript;

			// A confirmed segwit transaction for us
			var tx = CreateCreditingTransaction(NewScript("A"), Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx);

			// Another confirmed segwit transaction for us
			tx = CreateCreditingTransaction(NewScript("B"), Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx);

			var createdCoins = transactionProcessor.Coins.Select(x => x.GetCoin()).ToArray(); // 2 coins of 1.0 btc each

			// Spend the received coins
			var destinationScript = NewScript("myself");
			var changeScript = NewScript("Change myself");
			tx = CreateSpendingTransaction(createdCoins, destinationScript, changeScript); // spends 1.2btc
			tx.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;
			var relevant = transactionProcessor.Process(tx);
			Assert.True(relevant);

			// Another confirmed segwit transaction for us
			tx = CreateCreditingTransaction(NewScript("C"), Money.Coins(1.0m), height: 54322);
			transactionProcessor.Process(tx);

			// At this moment we have one 1.2btc and one 0.8btc replaceable coins and one 1.0btc final coin
			var latestCreatedCoin = Assert.Single(transactionProcessor.Coins.CreatedBy(tx.Transaction.GetHash()));
			var coinsToSpend = createdCoins.Concat(new[] { latestCreatedCoin.GetCoin() }).ToArray();

			// Spend them again with a different amount
			destinationScript = new Key().PubKey.WitHash.ScriptPubKey;  // spend to someone else
			tx = CreateSpendingTransaction(coinsToSpend, destinationScript, changeScript);
			relevant = transactionProcessor.Process(tx);

			Assert.True(relevant);
			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.True(coin.IsReplaceable);
		}

		[Fact]
		public async Task HandlesRbfWithLessCoinsAsync()
		{
			// Replaces a previous RBF transaction by a new one that contains one more input (higher fee)
			var transactionProcessor = await CreateTransactionProcessorAsync();
			Script NewScript(string label) => transactionProcessor.NewKey(label).P2wpkhScript;

			// A confirmed segwit transaction for us
			var tx = CreateCreditingTransaction(NewScript("A"), Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx);

			// Another confirmed segwit transaction for us
			tx = CreateCreditingTransaction(NewScript("B"), Money.Coins(1.0m), height: 54321);
			transactionProcessor.Process(tx);

			var createdCoins = transactionProcessor.Coins.Select(x => x.GetCoin()).ToArray(); // 2 coins of 1.0 btc each

			// Spend the received coins (replaceable)
			var destinationScript = NewScript("myself"); ;
			var changeScript = NewScript("Change myself");
			tx = CreateSpendingTransaction(createdCoins, destinationScript, changeScript); // spends 1.2btc
			tx.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;
			var relevant = transactionProcessor.Process(tx);
			Assert.True(relevant);

			// replace previous tx with another one spending only one coin
			destinationScript = new Key().PubKey.WitHash.ScriptPubKey;  // spend to someone else
			tx = CreateSpendingTransaction(createdCoins.Take(1), destinationScript, changeScript); // spends 1.2btc
			relevant = transactionProcessor.Process(tx);

			Assert.True(relevant);
			Assert.Equal(2, transactionProcessor.Coins.Count());
			//Assert.True(coin.IsReplaceable);
		}

		[Fact]
		public async Task ReceiveTransactionForWalletAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();
			SmartCoin receivedCoin = null;
			transactionProcessor.CoinReceived += (s, theCoin) => receivedCoin = theCoin;
			var keys = transactionProcessor.KeyManager.GetKeys();
			var tx = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(1.0m));

			var relevant = transactionProcessor.Process(tx);

			// It is relevant because is funding the wallet
			Assert.True(relevant);
			var coin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal(Money.Coins(1.0m), coin.Amount);
			Assert.True(transactionProcessor.TransactionStore.TryGetTransaction(tx.GetHash(), out _));
			Assert.NotNull(receivedCoin);
		}

		[Fact]
		public async Task SpendCoinAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();
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
			var coin = Assert.Single(transactionProcessor.Coins.AsAllCoinsView());
			Assert.False(coin.Unspent);
			Assert.NotNull(spentCoin);
			Assert.Equal(coin, spentCoin);
		}

		[Fact]
		public async Task ReceiveTransactionWithDustForWalletAsync()
		{
			var transactionProcessor = await CreateTransactionProcessorAsync();
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
			Assert.True(relevant);
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
			Assert.True(relevant);
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
			Assert.Equal("A", createdCoin.Clusters.Labels);

			// Spend the received coin to someone else B
			var changeScript1 = transactionProcessor.NewKey("B").P2wpkhScript;
			var tx1 = CreateSpendingTransaction(new[] { createdCoin.GetCoin() }, new Key().ScriptPubKey, changeScript1);
			transactionProcessor.Process(tx1);
			createdCoin = transactionProcessor.Coins.First();
			Assert.Equal("A, B", createdCoin.Clusters.Labels);

			// Spend the received coin to mylself else C
			var myselfScript = transactionProcessor.NewKey("C").P2wpkhScript;
			var changeScript2 = transactionProcessor.NewKey("").P2wpkhScript;
			var tx2 = CreateSpendingTransaction(new[] { createdCoin.GetCoin() }, myselfScript, changeScript2);
			transactionProcessor.Process(tx2);

			Assert.Equal(2, transactionProcessor.Coins.Count());
			createdCoin = transactionProcessor.Coins.First();
			Assert.Equal("A, B, C", createdCoin.Clusters.Labels);

			var createdchangeCoin = transactionProcessor.Coins.Last();
			Assert.Equal("A, B, C", createdchangeCoin.Clusters.Labels);
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

			var scoinA = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "A");
			var scoinB = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "B");
			var scoinC = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "C");

			var changeScript = transactionProcessor.NewKey("D").P2wpkhScript;
			var coins = new[] { scoinA.GetCoin(), scoinB.GetCoin(), scoinC.GetCoin() };
			var tx3 = CreateSpendingTransaction(coins, new Key().ScriptPubKey, changeScript);
			transactionProcessor.Process(tx3);

			var changeCoinD = Assert.Single(transactionProcessor.Coins);
			Assert.Equal("A, B, C, D", changeCoinD.Clusters.Labels);

			key = transactionProcessor.NewKey("E");
			var tx4 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx4);
			var scoinE = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "E");

			changeScript = transactionProcessor.NewKey("F").P2wpkhScript;
			coins = new[] { changeCoinD.GetCoin(), scoinE.GetCoin() };
			var tx5 = CreateSpendingTransaction(coins, new Key().ScriptPubKey, changeScript);
			transactionProcessor.Process(tx5);

			var changeCoin = Assert.Single(transactionProcessor.Coins);
			Assert.Equal("A, B, C, D, E, F", changeCoin.Clusters.Labels);
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

			var scoinA = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "A");
			var scoinB = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "B");
			var scoinC = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "C");

			var myself = transactionProcessor.NewKey("D").P2wpkhScript;
			var changeScript = transactionProcessor.NewKey("").P2wpkhScript;
			var coins = new[] { scoinA.GetCoin(), scoinB.GetCoin(), scoinC.GetCoin() };
			var tx3 = CreateSpendingTransaction(coins, myself, changeScript);
			transactionProcessor.Process(tx3);

			var paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
			Assert.Equal("A, B, C, D", paymentCoin.Clusters.Labels);

			var tx4 = CreateCreditingTransaction(myself, Money.Coins(7.0m));
			transactionProcessor.Process(tx4);
			Assert.Equal(2, transactionProcessor.Coins.Count(c => c.ScriptPubKey == myself));
			var newPaymentCoin = Assert.Single(transactionProcessor.Coins, c => c.Amount == Money.Coins(7.0m));
			Assert.Equal("A, B, C, D", newPaymentCoin.Clusters.Labels);
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

			var scoinA = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "A");
			var scoinB = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "B");
			var scoinC = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "C");

			var myself = transactionProcessor.NewKey("D").P2wpkhScript;
			var changeScript = transactionProcessor.NewKey("").P2wpkhScript;
			var coins = new[] { scoinA.GetCoin(), scoinB.GetCoin(), scoinC.GetCoin() };
			var tx3 = CreateSpendingTransaction(coins, myself, changeScript, replaceable: true);
			transactionProcessor.Process(tx3);

			var paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
			Assert.Equal("A, B, C, D", paymentCoin.Clusters.Labels);

			coins = new[] { scoinB.GetCoin(), scoinC.GetCoin(), scoinA.GetCoin() };
			var tx4 = CreateSpendingTransaction(coins, myself, changeScript);
			transactionProcessor.Process(tx4);

			paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
			Assert.Equal("A, B, C, D", paymentCoin.Clusters.Labels);
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

			var scoinA = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "A");
			var scoinB = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "B");
			var scoinC = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "C");

			var myself = transactionProcessor.NewKey("D").P2wpkhScript;
			var changeScript = transactionProcessor.NewKey("").P2wpkhScript;
			var coins = new[] { scoinA.GetCoin(), scoinB.GetCoin(), scoinC.GetCoin() };
			var tx3 = CreateSpendingTransaction(coins, myself, changeScript, replaceable: true);
			transactionProcessor.Process(tx3);

			var paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
			Assert.Equal("A, B, C, D", paymentCoin.Clusters.Labels);

			key = transactionProcessor.NewKey("X");
			var tx4 = CreateCreditingTransaction(key.P2wpkhScript, Money.Coins(1.0m));
			transactionProcessor.Process(tx4);
			var scoinX = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "X");

			coins = new[] { scoinB.GetCoin(), scoinX.GetCoin(), scoinC.GetCoin(), scoinA.GetCoin() };
			var tx5 = CreateSpendingTransaction(coins, myself, changeScript);
			transactionProcessor.Process(tx5);

			paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
			Assert.Equal("A, B, C, D, X", paymentCoin.Clusters.Labels);
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

			var scoinA = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "A");
			var scoinB = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "B");
			var scoinC = Assert.Single(transactionProcessor.Coins, c => c.Clusters.Labels == "C");

			var changeScript = transactionProcessor.NewKey("D").P2wpkhScript;
			var coins = new[] { scoinA.GetCoin(), scoinB.GetCoin(), scoinC.GetCoin() };
			var tx3 = CreateSpendingTransaction(coins, new Key().ScriptPubKey, changeScript, height: 55555);
			transactionProcessor.Process(tx3);

			var changeCoinD = Assert.Single(transactionProcessor.Coins);
			Assert.Equal("A, B, C, D", changeCoinD.Clusters.Labels);
			Assert.Equal(scoinA.Clusters, changeCoinD.Clusters);
			Assert.Equal(scoinB.Clusters, changeCoinD.Clusters);
			Assert.Equal(scoinC.Clusters, changeCoinD.Clusters);

			// reorg
			transactionProcessor.UndoBlock(tx3.Height);
			Assert.DoesNotContain(changeCoinD, transactionProcessor.Coins);
			Assert.Equal("A, B, C, D", changeCoinD.Clusters.Labels);
			Assert.Equal(scoinA.Clusters, changeCoinD.Clusters);
			Assert.Equal(scoinB.Clusters, changeCoinD.Clusters);
			Assert.Equal(scoinC.Clusters, changeCoinD.Clusters);
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

			Assert.Empty(anonymousCoin.Clusters.Labels);
			Assert.NotEmpty(changeCoin.Clusters.Labels);
		}

		private async Task<TransactionProcessor> CreateTransactionProcessorAsync([CallerMemberName] string callerName = "")
		{
			var keyManager = KeyManager.CreateNew(out _, "password");
			keyManager.AssertCleanKeysIndexed();

			var txStore = new AllTransactionStore();
			var dir = Path.Combine(Global.Instance.DataDir, callerName, "TransactionStore");
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
