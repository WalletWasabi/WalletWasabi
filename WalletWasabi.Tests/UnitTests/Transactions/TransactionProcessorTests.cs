using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

/// <summary>
/// Tests for <see cref="TransactionProcessor"/>.
/// </summary>
public class TransactionProcessorTests
{
	[Fact]
	public async Task TransactionDoesNotContainCoinsForTheWalletAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		// This transaction doesn't have any coin for the wallet. It is not relevant.
		var tx = CreateCreditingTransaction(BitcoinFactory.CreateScript(), Money.Coins(1.0m));

		var relevant = transactionProcessor.Process(tx);

		Assert.False(relevant.IsNews);
		Assert.Empty(transactionProcessor.Coins);
		Assert.True(transactionProcessor.TransactionStore.MempoolStore.IsEmpty());
		Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
	}

	[Fact]
	public async Task SpendToLegacyScriptsAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var keys = transactionProcessor.KeyManager.GetKeys().ToArray();

		// A payment to a key under our control but using P2PKH script (legacy)
		var tx = CreateCreditingTransaction(keys.First().PubKey.GetScriptPubKey(ScriptPubKeyType.Legacy), Money.Coins(1.0m));
		var relevant = transactionProcessor.Process(tx);

		Assert.False(relevant.IsNews);
		Assert.Empty(transactionProcessor.Coins);
		Assert.True(transactionProcessor.TransactionStore.MempoolStore.IsEmpty());
		Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
	}

	[Fact]
	public async Task UnconfirmedTransactionIsNotSegWitAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		// No segwit transaction. Ignore it.
		using Key key = new();
		var tx = CreateCreditingTransaction(key.PubKey.Hash.ScriptPubKey, Money.Coins(1.0m));

		var relevant = transactionProcessor.Process(tx);

		Assert.False(relevant.IsNews);
		Assert.Empty(transactionProcessor.Coins);
		Assert.True(transactionProcessor.TransactionStore.MempoolStore.IsEmpty());
		Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
	}

	[Fact]
	public async Task ConfirmedTransactionIsNotSegWitAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		// No segwit transaction. Ignore it.
		using Key key = new();
		var tx = CreateCreditingTransaction(key.PubKey.Hash.ScriptPubKey, Money.Coins(1.0m), height: 54321);

		var relevant = transactionProcessor.Process(tx);

		Assert.False(relevant.IsNews);
		Assert.Empty(transactionProcessor.Coins);
		Assert.True(transactionProcessor.TransactionStore.MempoolStore.IsEmpty());
		Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
	}

	[Fact]
	public async Task ProcessResultAfterConfirmationCorrectAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		// An unconfirmed segwit transaction for us
		var hdPubKey = transactionProcessor.KeyManager.GetKeys().First();

		var tx1 = CreateCreditingTransaction(hdPubKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit), Money.Coins(1.0m));
		var blockHeight = new Height(77551);
		var tx2 = new SmartTransaction(tx1.Transaction, blockHeight);
		var tx3 = CreateSpendingTransaction(tx2.Transaction.Outputs.AsCoins().First(), BitcoinFactory.CreateScript());
		var blockHeight2 = new Height(77552);
		var tx4 = new SmartTransaction(tx3.Transaction, blockHeight2);
		var results = transactionProcessor.Process(tx1, tx2, tx3, tx4).ToArray();
		var res1 = results[0];
		var res2 = results[1];
		var res3 = results[2];
		var res4 = results[3];

		Assert.False(res1.IsOwnCoinJoin);
		Assert.Empty(res1.NewlyConfirmedReceivedCoins);
		Assert.Empty(res1.NewlyConfirmedSpentCoins);
		Assert.Single(res1.NewlyReceivedCoins);
		Assert.Empty(res1.NewlySpentCoins);
		Assert.Single(res1.ReceivedCoins);
		Assert.Empty(res1.SpentCoins);
		Assert.Empty(res1.ReceivedDusts);
		Assert.Empty(res1.ReplacedCoins);
		Assert.Empty(res1.RestoredCoins);
		Assert.Empty(res1.SuccessfullyDoubleSpentCoins);
		Assert.True(res1.IsNews);
		Assert.NotNull(res1.Transaction);

		Assert.False(res2.IsOwnCoinJoin);
		Assert.Single(res2.NewlyConfirmedReceivedCoins);
		Assert.Empty(res2.NewlyConfirmedSpentCoins);
		Assert.Empty(res2.NewlyReceivedCoins);
		Assert.Empty(res2.NewlySpentCoins);
		Assert.Single(res2.ReceivedCoins);
		Assert.Empty(res2.SpentCoins);
		Assert.Empty(res2.ReceivedDusts);
		Assert.Empty(res2.ReplacedCoins);
		Assert.Empty(res2.RestoredCoins);
		Assert.Empty(res2.SuccessfullyDoubleSpentCoins);
		Assert.True(res2.IsNews);
		Assert.NotNull(res2.Transaction);

		Assert.False(res3.IsOwnCoinJoin);
		Assert.Empty(res3.NewlyConfirmedReceivedCoins);
		Assert.Empty(res3.NewlyConfirmedSpentCoins);
		Assert.Empty(res3.NewlyReceivedCoins);
		Assert.Single(res3.NewlySpentCoins);
		Assert.Empty(res3.ReceivedCoins);
		Assert.Single(res3.SpentCoins);
		Assert.Empty(res3.ReceivedDusts);
		Assert.Empty(res3.ReplacedCoins);
		Assert.Empty(res3.RestoredCoins);
		Assert.Empty(res3.SuccessfullyDoubleSpentCoins);
		Assert.True(res3.IsNews);
		Assert.NotNull(res3.Transaction);

		Assert.False(res4.IsOwnCoinJoin);
		Assert.Empty(res4.NewlyConfirmedReceivedCoins);
		Assert.Single(res4.NewlyConfirmedSpentCoins);
		Assert.Empty(res4.NewlyReceivedCoins);
		Assert.Single(res4.NewlySpentCoins);
		Assert.Empty(res4.ReceivedCoins);
		Assert.Single(res4.SpentCoins);
		Assert.Empty(res4.ReceivedDusts);
		Assert.Empty(res4.ReplacedCoins);
		Assert.Empty(res4.RestoredCoins);
		Assert.Empty(res4.SuccessfullyDoubleSpentCoins);
		Assert.True(res4.IsNews);
		Assert.NotNull(res4.Transaction);
	}

	[Fact]
	public async Task UpdateTransactionHeightAfterConfirmationAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		var hdPubKey = transactionProcessor.KeyManager.GetKeys().First();

		// An unconfirmed segwit transaction for us
		var tx = CreateCreditingTransaction(hdPubKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit), Money.Coins(1.0m));
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

	/// <summary>
	/// Make sure that coins with <see cref="HdPubKey"/>s are tracked and that we track latest spending heights for <see cref="HdPubKey"/>s as well.
	/// </summary>
	[Fact]
	public async Task RememberLatestSpendingHeightAsync()
	{
		// --tx0---> (A) ----tx1----> (pay to B)

		await using AllTransactionStore txStore = await CreateTransactionStoreAsync();
		TransactionProcessor transactionProcessor = CreateTransactionProcessor(txStore);
		HdPubKey hdPubKey = transactionProcessor.NewKey("A");

		Assert.False(transactionProcessor.Coins.HasUnspentCoin(hdPubKey));

		SmartTransaction tx0 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m), height: 54321);
		transactionProcessor.Process(tx0);

		Assert.True(transactionProcessor.Coins.HasUnspentCoin(hdPubKey));
		Assert.Null(hdPubKey.LatestSpendingHeight);

		SmartCoin coinA = Assert.Single(transactionProcessor.Coins);
		Script changeScript = transactionProcessor.NewKey("B").P2wpkhScript;

		using Key key = new();
		SmartTransaction tx1 = CreateSpendingTransaction(new[] { coinA.Coin }, key.PubKey.ScriptPubKey, changeScript, height: 55555);
		transactionProcessor.Process(tx1);

		SmartCoin changeCoinB = Assert.Single(transactionProcessor.Coins);
		Assert.False(transactionProcessor.Coins.HasUnspentCoin(hdPubKey));
		Assert.Equal(new Height(55555), hdPubKey.LatestSpendingHeight);
	}

	[Fact]
	public async Task IgnoreDoubleSpendAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		var keys = transactionProcessor.KeyManager.GetKeys().ToArray();

		// An unconfirmed segwit transaction for us
		var tx0 = CreateCreditingTransaction(keys[0].PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit), Money.Coins(1.0m));

		var createdCoin = tx0.Transaction.Outputs.AsCoins().First();

		// Spend the received coin
		var tx1 = CreateSpendingTransaction(createdCoin, keys[1].PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));

		// Spend the same coin again
		var tx2 = CreateSpendingTransaction(createdCoin, keys[2].PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));
		var relevant = transactionProcessor.Process(tx0, tx1, tx2).Last();

		Assert.False(relevant.IsNews);
		Assert.Single(transactionProcessor.Coins, coin => !coin.IsSpent());
		Assert.Single(transactionProcessor.Coins.AsAllCoinsView(), coin => coin.IsSpent());

		// Transaction store assertions
		Assert.True(transactionProcessor.TransactionStore.ConfirmedStore.IsEmpty());
		var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
		Assert.Equal(2, mempool.Count);
		Assert.Equal(tx0, mempool.First());
		Assert.Equal(tx1, mempool.Last());
	}

	[Fact]
	public async Task ConfirmedDoubleSpendAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		var keys = transactionProcessor.KeyManager.GetKeys().ToArray();

		int doubleSpendReceived = 0;
		transactionProcessor.WalletRelevantTransactionProcessed += (s, e) =>
		{
			var doubleSpentCoins = e.SuccessfullyDoubleSpentCoins;
			if (doubleSpentCoins.Count != 0)
			{
				var coin = Assert.Single(doubleSpentCoins);

				// Double spend to ourselves but to a different address. So checking the address.
				Assert.Equal(keys[1].GetAssumedScriptPubKey(), coin.ScriptPubKey);

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
					case 0: Assert.Equal(keys[0].GetAssumedScriptPubKey(), c.ScriptPubKey); break;
					case 1: Assert.Equal(keys[1].GetAssumedScriptPubKey(), c.ScriptPubKey); break;
					case 2: Assert.Equal(keys[2].GetAssumedScriptPubKey(), c.ScriptPubKey); break;
					default:
						throw new InvalidOperationException();
				}

				coinReceivedCalled++;
			}
		};

		// An unconfirmed segwit transaction for us
		var tx0 = CreateCreditingTransaction(keys[0].GetAssumedScriptPubKey(), Money.Coins(1.0m), height: 54321);

		var createdCoin = tx0.Transaction.Outputs.AsCoins().First();

		// Spend the received coin
		var tx1 = CreateSpendingTransaction(createdCoin, keys[1].GetAssumedScriptPubKey());

		Assert.Equal(0, doubleSpendReceived);

		// Spend the coin
		var tx2 = CreateSpendingTransaction(createdCoin, keys[2].GetAssumedScriptPubKey(), height: 54321);
		var relevant = transactionProcessor.Process(tx0, tx1, tx2).Last();
		Assert.Equal(1, doubleSpendReceived);

		Assert.True(relevant.IsNews);
		Assert.Single(transactionProcessor.Coins, coin => !coin.IsSpent() && coin.Confirmed);
		Assert.Single(transactionProcessor.Coins.AsAllCoinsView(), coin => coin.IsSpent() && coin.Confirmed);

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

		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		int replaceTransactionReceivedCalled = 0;
		transactionProcessor.WalletRelevantTransactionProcessed += (s, e) =>
		{
			if (e.ReplacedCoins.Count != 0 || e.RestoredCoins.Count != 0)
			{
				if (e.RestoredCoins.Count != 0)
				{
					// Move the original coin from spent to unspent - so add.
					var originalCoin = Assert.Single(e.RestoredCoins);
					Assert.Equal(Money.Coins(1.0m), originalCoin.Amount);
				}

				// Remove the created coin by the transaction.
				Assert.Equal(3, e.ReplacedCoins.Count);
				Assert.Single(e.ReplacedCoins, coin => coin.HdPubKey.Labels == "B");
				Assert.Single(e.ReplacedCoins, coin => coin.HdPubKey.Labels == "C");
				Assert.Single(e.ReplacedCoins, coin => coin.HdPubKey.Labels == "D");

				replaceTransactionReceivedCalled++;
			}
		};

		// A confirmed segwit transaction for us
		var tx0 = CreateCreditingTransaction(transactionProcessor.NewKey("A").P2wpkhScript, Money.Coins(1.0m), height: 54321);

		var createdCoin = tx0.Transaction.Outputs.AsCoins().First();

		// Spend the received coin
		var tx1 = CreateSpendingTransaction(createdCoin, transactionProcessor.NewKey("B").P2wpkhScript);
		tx1.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;
		tx1.Transaction.Outputs[0].Value = Money.Coins(0.95m);
		tx1.Transaction.Outputs.Add(Money.Coins(0.1m), transactionProcessor.NewKey("C").P2wpkhScript);
		var relevant1 = transactionProcessor.Process(tx0, tx1).Last();
		Assert.True(relevant1.IsNews);
		Assert.Equal(0, replaceTransactionReceivedCalled);

		var unconfirmedCoin1 = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "B");
		var unconfirmedCoin2 = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "C");
		Assert.True(unconfirmedCoin1.Transaction.IsRBF);
		Assert.True(unconfirmedCoin2.Transaction.IsRBF);

		// Spend the received coin
		var tx2 = CreateSpendingTransaction(unconfirmedCoin1.Coin, transactionProcessor.NewKey("D").P2wpkhScript);
		tx2.Transaction.Outputs[0].Value = Money.Coins(0.7m);
		var relevant2 = transactionProcessor.Process(tx2);
		Assert.True(relevant2.IsNews);
		Assert.Equal(0, replaceTransactionReceivedCalled);

		// Spend the coin
		var tx3 = CreateSpendingTransaction(createdCoin, transactionProcessor.NewKey("E").P2wpkhScript);
		tx3.Transaction.Outputs[0].Value = Money.Coins(0.9m);
		var relevant3 = transactionProcessor.Process(tx3);

		Assert.True(relevant3.IsNews);
		Assert.Equal(1, replaceTransactionReceivedCalled);
		var finalCoin = Assert.Single(transactionProcessor.Coins);
		Assert.True(finalCoin.Transaction.IsRBF);
		Assert.Equal("E", finalCoin.HdPubKey.Labels);

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
	public async Task HandlesConfirmedReplacedTransactionAsync()
	{
		// --tx0---> (A) --tx1 (replaceable)-+--> (B) --tx2---> (D)
		//                  |                |
		//                  |                +--> (C)
		//                  |
		//                  +--tx3 (replacement)---> (E)   { after this tx1 is confirmed }
		//
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		// A confirmed segwit transaction for us
		var tx0 = CreateCreditingTransaction(transactionProcessor.NewKey("A").P2wpkhScript, Money.Coins(1.0m), height: 54321);
		var createdCoin = tx0.Transaction.Outputs.AsCoins().First();
		transactionProcessor.Process(tx0);

		// Spend the received coin
		var tx1 = CreateSpendingTransaction(createdCoin, transactionProcessor.NewKey("B").P2wpkhScript);
		tx1.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;
		tx1.Transaction.Outputs[0].Value = Money.Coins(0.95m);
		tx1.Transaction.Outputs.Add(Money.Coins(0.1m), transactionProcessor.NewKey("C").P2wpkhScript);
		transactionProcessor.Process(tx1);

		var unconfirmedCoin1 = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "B");
		var unconfirmedCoin2 = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "C");

		// Spend the received coin
		var tx2 = CreateSpendingTransaction(unconfirmedCoin1.Coin, transactionProcessor.NewKey("D").P2wpkhScript);
		tx2.Transaction.Outputs[0].Value = Money.Coins(0.7m);
		transactionProcessor.Process(tx2);

		// Spend the replaceable coin
		var tx3 = CreateSpendingTransaction(createdCoin, transactionProcessor.NewKey("E").P2wpkhScript);
		tx3.Transaction.Outputs[0].Value = Money.Coins(0.9m);
		transactionProcessor.Process(tx3);

		// Now it is confirmed
		var blockHeight = new Height(77551);
		tx1 = new SmartTransaction(tx1.Transaction, blockHeight);
		var relevant = transactionProcessor.Process(tx1);

		var coin1 = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "B");
		var coin2 = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "C");

		Assert.True(coin1.Confirmed);
		Assert.True(coin2.Confirmed);

		Assert.DoesNotContain(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "E");
		Assert.DoesNotContain(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "D"); // Wasabi forgot about it but that's not a problem.

		// Replacement transaction tx3 has to be removed because tx1 confirmed and then it is invalid.
		var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
		Assert.DoesNotContain(tx3, mempool);
	}

	[Fact]
	public async Task RecognizeReplaceableCoinsCorrectlyAsync()
	{
		// --tx0 ---> (A) -(replaceable)--tx1 -+--> (B) --tx2---> (D)
		//                                     |
		//                                     +--> (C)
		//
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		// A confirmed segwit transaction for us
		var tx0 = CreateCreditingTransaction(transactionProcessor.NewKey("A").P2wpkhScript, Money.Coins(1.0m));
		var createdCoin = tx0.Transaction.Outputs.AsCoins().First();
		transactionProcessor.Process(tx0);

		// Spend the received coin
		var tx1 = CreateSpendingTransaction(createdCoin, transactionProcessor.NewKey("B").P2wpkhScript);
		tx1.Transaction.Outputs[0].Value = Money.Coins(0.95m);
		tx1.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;
		tx1.Transaction.Outputs.Add(Money.Coins(0.1m), transactionProcessor.NewKey("C").P2wpkhScript);
		transactionProcessor.Process(tx1);

		var coinB = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "B");
		var coinC = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "C");

		// Spend the received coin
		var tx2 = CreateSpendingTransaction(coinB.Coin, transactionProcessor.NewKey("D").P2wpkhScript);
		tx2.Transaction.Outputs[0].Value = Money.Coins(0.7m);
		transactionProcessor.Process(tx2);

		var coinD = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "D");

		Assert.True(coinB.Transaction.IsRBF);
		Assert.True(coinC.Transaction.IsRBF);
		Assert.True(coinD.Transaction.IsRBF);

		// Now it is confirmed
		var blockHeight = new Height(77551);
		tx1 = new SmartTransaction(tx1.Transaction, blockHeight);
		var relevant = transactionProcessor.Process(tx1);

		coinC = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "C");
		coinD = Assert.Single(transactionProcessor.Coins, coin => coin.HdPubKey.Labels == "D");

		Assert.False(coinC.Transaction.IsRBF);
		Assert.False(coinD.Transaction.IsRBF);
	}

	[Fact]
	public async Task ConfirmTransactionTestAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		var keys = transactionProcessor.KeyManager.GetKeys().ToArray();
		int confirmed = 0;
		transactionProcessor.WalletRelevantTransactionProcessed += (s, e) =>
		{
			if (e.NewlySpentCoins.Count != 0)
			{
				throw new InvalidOperationException("We are not spending the coin.");
			}
			else if (e.NewlyConfirmedSpentCoins.Count != 0)
			{
				confirmed++;
			}
		};

		// A confirmed segwit transaction for us
		var tx1 = CreateCreditingTransaction(keys[0].PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit), Money.Coins(1.0m));
		var results = transactionProcessor.Process(tx1, tx1).ToArray();
		var res1 = results[0];
		var res2a = results[1];

		// Process it again.
		var res2b = transactionProcessor.Process(tx1);

		Assert.True(res1.IsNews);
		Assert.Single(res1.NewlyReceivedCoins);
		Assert.Single(res1.ReceivedCoins);
		Assert.Empty(res1.NewlyConfirmedReceivedCoins);
		Assert.Empty(res1.ReceivedDusts);

		foreach (var res2 in new[] { res2a, res2b })
		{
			Assert.False(res2.IsNews);
			Assert.Empty(res2.ReplacedCoins);
			Assert.Empty(res2.RestoredCoins);
			Assert.Empty(res2.SuccessfullyDoubleSpentCoins);
			Assert.Single(res2.ReceivedCoins);
			Assert.Empty(res2.NewlyConfirmedReceivedCoins);
			Assert.Empty(res2.ReceivedDusts);
		}

		var coin = Assert.Single(transactionProcessor.Coins);
		Assert.False(coin.Confirmed);

		var tx2 = new SmartTransaction(tx1.Transaction.Clone(), new Height(54321));

		Assert.Equal(tx1.GetHash(), tx2.GetHash());
		var res3 = transactionProcessor.Process(tx2);
		Assert.True(res3.IsNews);
		Assert.Empty(res3.ReplacedCoins);
		Assert.Empty(res3.RestoredCoins);
		Assert.Empty(res3.SuccessfullyDoubleSpentCoins);
		Assert.Single(res3.ReceivedCoins);
		Assert.Single(res3.NewlyConfirmedReceivedCoins);
		Assert.Empty(res3.ReceivedDusts);
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

		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		Script NewScript(string label) => transactionProcessor.NewKey(label).P2wpkhScript;

		// A confirmed segwit transaction for us
		var tx0 = CreateCreditingTransaction(NewScript("A"), Money.Coins(1.0m), height: 54321);

		// Another confirmed segwit transaction for us
		var tx1 = CreateCreditingTransaction(NewScript("B"), Money.Coins(1.0m), height: 54321);
		transactionProcessor.Process(tx0, tx1);

		var createdCoins = transactionProcessor.Coins.Select(x => x.Coin).ToArray(); // 2 coins of 1.0 btc each

		// Spend the received coins
		var destinationScript = NewScript("myself");
		var changeScript = NewScript("Change myself");
		var tx2 = CreateSpendingTransaction(createdCoins, destinationScript, changeScript); // spends 1.2btc
		tx2.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;

		// Another confirmed segwit transaction for us
		var tx3 = CreateCreditingTransaction(NewScript("C"), Money.Coins(1.0m), height: 54322);

		var relevant1 = transactionProcessor.Process(tx2, tx3).First();
		Assert.True(relevant1.IsNews);

		// At this moment we have one 1.2btc and one 0.8btc replaceable coins and one 1.0btc final coin
		var latestCreatedCoin = Assert.Single(transactionProcessor.Coins.CreatedBy(tx3.Transaction.GetHash()));
		var coinsToSpend = createdCoins.Concat(new[] { latestCreatedCoin.Coin }).ToArray();

		// Spend them again with a different amount
		var destinationScript2 = BitcoinFactory.CreateScript(); // spend to someone else
		var tx4 = CreateSpendingTransaction(coinsToSpend, destinationScript2, changeScript);
		var relevant2 = transactionProcessor.Process(tx4);

		Assert.True(relevant2.IsNews);
		var coin = Assert.Single(transactionProcessor.Coins);
		Assert.True(coin.Transaction.IsRBF);

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

		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		Script NewScript(string label) => transactionProcessor.NewKey(label).P2wpkhScript;

		// A confirmed segwit transaction for us
		var tx0 = CreateCreditingTransaction(NewScript("A"), Money.Coins(1.0m), height: 54321);
		transactionProcessor.Process(tx0);

		// Another confirmed segwit transaction for us
		var tx1 = CreateCreditingTransaction(NewScript("B"), Money.Coins(1.0m), height: 54321);
		transactionProcessor.Process(tx1);

		var createdCoins = transactionProcessor.Coins.Select(x => x.Coin).ToArray(); // 2 coins of 1.0 btc each

		// Spend the received coins (replaceable)
		var destinationScript = NewScript("myself");
		var changeScript = NewScript("Change myself");
		var tx2 = CreateSpendingTransaction(createdCoins, destinationScript, changeScript); // spends 1.2btc
		tx2.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;
		var relevant = transactionProcessor.Process(tx2);
		Assert.True(relevant.IsNews);

		// replace previous tx with another one spending only one coin
		destinationScript = BitcoinFactory.CreateScript(); // spend to someone else
		var tx3 = CreateSpendingTransaction(createdCoins.Take(1), destinationScript, changeScript); // spends 0.6btc
		relevant = transactionProcessor.Process(tx3);

		Assert.True(relevant.IsNews);
		var replaceableCoin = Assert.Single(transactionProcessor.Coins, c => c.Transaction.IsRBF);
		Assert.Equal(tx3.Transaction.GetHash(), replaceableCoin.TransactionId);

		var nonReplaceableCoin = Assert.Single(transactionProcessor.Coins, c => !c.Transaction.IsRBF);
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
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		SmartCoin? receivedCoin = null;
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
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		SmartCoin? spentCoin = null;
		transactionProcessor.WalletRelevantTransactionProcessed += (s, e) =>
		{
			if (e.NewlySpentCoins.Count != 0)
			{
				spentCoin = e.NewlySpentCoins.Single();
			}
		};
		var keys = transactionProcessor.KeyManager.GetKeys();
		var tx0 = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx0);

		var createdCoin = tx0.Transaction.Outputs.AsCoins().First();

		// Spend the received coin
		using Key key = new();
		var tx1 = CreateSpendingTransaction(createdCoin, key.PubKey.ScriptPubKey);
		var relevant = transactionProcessor.Process(tx1);

		Assert.True(relevant.IsNews);
		var coin = Assert.Single(transactionProcessor.Coins.AsAllCoinsView());
		Assert.True(coin.IsSpent());
		Assert.NotNull(spentCoin);
		Assert.Equal(coin, spentCoin);

		// Transaction store assertions
		var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
		Assert.Equal(2, mempool.Count);

		var matureTxs = transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions().ToArray();
		Assert.Empty(matureTxs);
	}

	[Fact]
	public async Task CorrectSpenderAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		SmartCoin? spentCoin = null;
		transactionProcessor.WalletRelevantTransactionProcessed += (s, e) =>
		{
			if (e.NewlySpentCoins.Count != 0)
			{
				spentCoin = e.NewlySpentCoins.Single();
			}
		};
		var keys = transactionProcessor.KeyManager.GetKeys();
		var tx0 = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx0);

		var createdCoin = tx0.Transaction.Outputs.AsCoins().First();

		// Spend the received coin
		using Key key = new();
		var tx1 = CreateSpendingTransaction(createdCoin, key.PubKey.ScriptPubKey);

		transactionProcessor.Process(tx1);

		var tx2 = new SmartTransaction(tx1.Transaction, tx1.Height, tx1.BlockHash, tx1.BlockIndex, tx1.Labels, tx1.IsReplacement, tx1.IsSpeedup, tx1.IsCancellation, tx1.FirstSeen);
		var relevant = transactionProcessor.Process(tx2);

		Assert.False(relevant.IsNews);
		var coin = Assert.Single(transactionProcessor.Coins.AsAllCoinsView());
		Assert.True(coin.IsSpent());
		Assert.NotNull(spentCoin);
		Assert.Equal(coin, spentCoin);

		// Transaction store assertions
		var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
		Assert.Equal(2, mempool.Count);
		Assert.Contains(tx0, mempool);
		Assert.Contains(tx1, mempool);
		Assert.Contains(tx2, mempool);

		var matureTxs = transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions().ToArray();
		Assert.Empty(matureTxs);

		Assert.Contains(spentCoin, tx1.WalletInputs);
	}

	[Fact]
	public async Task CorrectCoinReferenceAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var tx0 = CreateCreditingTransaction(transactionProcessor.KeyManager.GetKeys().First().P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx0);
		var createdCoin = tx0.Transaction.Outputs.AsCoins().First();

		// Spend the received coin
		var tx1 = CreateSpendingTransaction(createdCoin, BitcoinFactory.CreateScript());
		tx1.Labels = "foo";

		// Add the transaction to the tx store manually and don't process it.
		transactionProcessor.TransactionStore.AddOrUpdate(tx1);

		var tx2 = new SmartTransaction(tx1.Transaction, tx1.Height, tx1.BlockHash, tx1.BlockIndex, tx1.Labels, tx1.IsReplacement, tx1.IsSpeedup, tx1.IsCancellation, tx1.FirstSeen);
		tx2.Labels = "bar";
		transactionProcessor.Process(tx2);

		// Ensure even if only tx2 was processed, the reference of the registered spender is to tx1
		// and that the labels were merged.
		var txid = tx1.GetHash();
		SmartTransaction? registeredSpender = transactionProcessor.Coins.AsAllCoinsView().SpentBy(txid).Single().SpenderTransaction;
		Assert.NotNull(registeredSpender);

		Assert.Same(tx1, registeredSpender);
		Assert.NotSame(tx2, registeredSpender);
		Assert.Contains("foo", registeredSpender.Labels.Select(x => x.ToString()));
		Assert.Contains("bar", registeredSpender.Labels.Select(x => x.ToString()));
	}

	[Fact]
	public async Task ReceiveTransactionWithDustForWalletAsync()
	{
		static void AssertCoin(ProcessedResult result, bool expectDust)
		{
			if (!expectDust)
			{
				Assert.Empty(result.ReceivedDusts);
				Assert.NotEmpty(result.ReceivedCoins);
				Assert.NotEmpty(result.NewlyReceivedCoins);
			}
			else
			{
				Assert.NotEmpty(result.ReceivedDusts);
				Assert.Empty(result.ReceivedCoins);
				Assert.Empty(result.NewlyReceivedCoins);
			}
			Assert.True(result.IsNews);
			Assert.Empty(result.NewlyConfirmedReceivedCoins);
		}

		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var keys = transactionProcessor.KeyManager.GetKeys();
		var tx = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(0.000099m));

		var relevant = transactionProcessor.Process(tx);

		// It is relevant even when all the coins can be dust.
		AssertCoin(relevant, expectDust: false);
		Assert.Single(transactionProcessor.Coins);

		// Transaction store assertions
		var mempool = transactionProcessor.TransactionStore.MempoolStore.GetTransactions();
		Assert.Single(mempool); // it doesn't matter if it is a dust only tx, we save the tx anyway.

		var matureTxs = transactionProcessor.TransactionStore.ConfirmedStore.GetTransactions().ToArray();
		Assert.Empty(matureTxs);

		var attackTx = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(0.000099m));

		var result = transactionProcessor.Process(attackTx);

		// It is relevant even when all the coins can be dust.
		AssertCoin(result, expectDust: true);
		Assert.Single(transactionProcessor.Coins); // the dust coin used is not added to the coin registry
	}

	[Fact]
	public async Task ReceiveManyConsecutiveTransactionWithDustForWalletAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var keys = transactionProcessor.KeyManager.GetKeys();

		foreach (var hdPubKey in keys.Take(5))
		{
			transactionProcessor.Process(
				CreateCreditingTransaction(hdPubKey.GetAssumedScriptPubKey(), Money.Coins(0.000099m)));
		}

		// It is relevant even when all the coins can be dust.
		Assert.All(keys.Take(5), key => Assert.Equal(KeyState.Used, key.KeyState));
		Assert.All(keys.Skip(5), key => Assert.Equal(KeyState.Clean, key.KeyState));
	}

	[Fact]
	public async Task ReceiveTransactionWithDustFromOurselvesAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);

		// The dust coin should raise an event, but it shouldn't be fully processed.
		transactionProcessor.WalletRelevantTransactionProcessed += (s, e)
			=> Assert.Empty(e.ReceivedDusts); // small coins received from us are not an attack.

		var keys = transactionProcessor.KeyManager.GetKeys();
		var creditingTx = CreateCreditingTransaction(keys.First().P2wpkhScript, Money.Coins(0.0001001m));
		var relevant = transactionProcessor.Process(creditingTx);
		var receivedCoin = Assert.Single(relevant.ReceivedCoins);

		using var destinationKey = new Key();
		var spendingTx = CreateSpendingTransaction(
			Enumerable.Repeat(receivedCoin.Coin, 1),
			destinationKey.GetScriptPubKey(ScriptPubKeyType.Legacy),
			transactionProcessor.KeyManager.GetNextReceiveKey(new LabelsArray("someone")).P2wpkhScript);
		relevant = transactionProcessor.Process(spendingTx);
		Assert.True(relevant.IsNews);

		// There is one coin (the change of the payment) under dust.
		var underDustThresholdCoin = Assert.Single(transactionProcessor.Coins);
		Assert.True(underDustThresholdCoin.Amount < transactionProcessor.DustThreshold);
	}

	[Fact]
	public async Task ReceiveCoinJoinTransactionAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var keys = transactionProcessor.KeyManager.GetKeys();

		var amount = Money.Coins(0.1m);

		var tx = Network.RegTest.CreateTransaction();
		tx.Version = 1;
		tx.LockTime = LockTime.Zero;
		tx.Outputs.Add(amount, keys.Skip(1).First().P2wpkhScript);
		tx.Outputs.AddRange(CreateRandomIndistinguishableOutputs(amount, count: 5));
		tx.Inputs.AddRange(CreateRandomInputs(count: 4));
		var relevant = transactionProcessor.Process(new SmartTransaction(tx, Height.Mempool));

		// It is relevant even when all the coins can be dust.
		Assert.True(relevant.IsNews);
		var coin = Assert.Single(transactionProcessor.Coins);
		Assert.Equal(1, coin.HdPubKey.AnonymitySet);
		Assert.Equal(amount, coin.Amount);
	}

	[Fact]
	public async Task ReceiveWasabiCoinJoinTransactionAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var keys = transactionProcessor.KeyManager.GetKeys();
		var amount = Money.Coins(0.1m);

		var stx = CreateCreditingTransaction(keys.First().P2wpkhScript, amount);
		transactionProcessor.Process(stx);

		var createdCoin = stx.Transaction.Outputs.AsCoins().First();

		var tx = Network.RegTest.CreateTransaction();
		tx.Version = 1;
		tx.LockTime = LockTime.Zero;
		tx.Outputs.Add(amount, keys.Skip(1).First().P2wpkhScript);
		tx.Outputs.AddRange(CreateRandomIndistinguishableOutputs(amount, 5));
		tx.Inputs.Add(createdCoin.Outpoint, Script.Empty, WitScript.Empty);
		tx.Inputs.AddRange(CreateRandomInputs(4));

		var relevant = transactionProcessor.Process(new SmartTransaction(tx, Height.Mempool));

		// It is relevant even when all the coins can be dust.
		Assert.True(relevant.IsNews);
		var coin = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.AnonymitySet > 1);
		Assert.Equal(5, coin.HdPubKey.AnonymitySet);
		Assert.Equal(amount, coin.Amount);
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
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var hdPubKey = transactionProcessor.NewKey("A");
		var tx0 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx0);

		var createdCoin = transactionProcessor.Coins.First();
		Assert.Equal("A", createdCoin.HdPubKey.Cluster.Labels);

		// Spend the received coin to someone else B
		var changeScript1 = transactionProcessor.NewKey("B").P2wpkhScript;
		using Key key = new();
		var tx1 = CreateSpendingTransaction(new[] { createdCoin.Coin }, key.PubKey.ScriptPubKey, changeScript1);
		transactionProcessor.Process(tx1);
		createdCoin = transactionProcessor.Coins.First();
		Assert.Equal("A, B", createdCoin.HdPubKey.Cluster.Labels);

		// Spend the received coin to myself else C
		var myselfScript = transactionProcessor.NewKey("C").P2wpkhScript;
		var changeScript2 = transactionProcessor.NewKey("").P2wpkhScript;
		var tx2 = CreateSpendingTransaction(new[] { createdCoin.Coin }, myselfScript, changeScript2);
		transactionProcessor.Process(tx2);

		Assert.Equal(2, transactionProcessor.Coins.Count());
		createdCoin = transactionProcessor.Coins.First();
		Assert.Equal("A, B, C", createdCoin.HdPubKey.Cluster.Labels);

		var createdChangeCoin = transactionProcessor.Coins.Last();
		Assert.Equal("A, B, C", createdChangeCoin.HdPubKey.Cluster.Labels);
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

		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var hdPubKey = transactionProcessor.NewKey("A");
		var tx0 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx0);

		hdPubKey = transactionProcessor.NewKey("B");
		var tx1 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx1);

		hdPubKey = transactionProcessor.NewKey("C");
		var tx2 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx2);

		var smartCoinA = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "A");
		var smartCoinB = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "B");
		var smartCoinC = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "C");

		var changeScript = transactionProcessor.NewKey("D").P2wpkhScript;
		var coins = new[] { smartCoinA.Coin, smartCoinB.Coin, smartCoinC.Coin };
		using Key key1 = new();
		var tx3 = CreateSpendingTransaction(coins, key1.PubKey.ScriptPubKey, changeScript);
		transactionProcessor.Process(tx3);

		var changeCoinD = Assert.Single(transactionProcessor.Coins);
		Assert.Equal("A, B, C, D", changeCoinD.HdPubKey.Cluster.Labels);

		hdPubKey = transactionProcessor.NewKey("E");
		var tx4 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx4);
		var smartCoinE = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "E");

		changeScript = transactionProcessor.NewKey("F").P2wpkhScript;
		coins = new[] { changeCoinD.Coin, smartCoinE.Coin };
		using Key key2 = new();
		var tx5 = CreateSpendingTransaction(coins, key2.PubKey.ScriptPubKey, changeScript);
		transactionProcessor.Process(tx5);

		var changeCoin = Assert.Single(transactionProcessor.Coins);
		Assert.Equal("A, B, C, D, E, F", changeCoin.HdPubKey.Cluster.Labels);
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

		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var hdPubKey = transactionProcessor.NewKey("A");
		var tx0 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx0);

		hdPubKey = transactionProcessor.NewKey("B");
		var tx1 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx1);

		hdPubKey = transactionProcessor.NewKey("C");
		var tx2 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx2);

		var smartCoinA = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "A");
		var smartCoinB = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "B");
		var smartCoinC = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "C");

		var myself = transactionProcessor.NewKey("D").P2wpkhScript;
		var changeScript = transactionProcessor.NewKey("").P2wpkhScript;
		var coins = new[] { smartCoinA.Coin, smartCoinB.Coin, smartCoinC.Coin };
		var tx3 = CreateSpendingTransaction(coins, myself, changeScript);
		transactionProcessor.Process(tx3);

		var paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
		Assert.Equal("A, B, C, D", paymentCoin.HdPubKey.Cluster.Labels);

		var tx4 = CreateCreditingTransaction(myself, Money.Coins(7.0m));
		transactionProcessor.Process(tx4);
		Assert.Equal(2, transactionProcessor.Coins.Count(c => c.ScriptPubKey == myself));
		var newPaymentCoin = Assert.Single(transactionProcessor.Coins, c => c.Amount == Money.Coins(7.0m));
		Assert.Equal("A, B, C, D", newPaymentCoin.HdPubKey.Cluster.Labels);
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

		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var hdPubKey = transactionProcessor.NewKey("A");
		var tx0 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx0);

		var tx1 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		var result = transactionProcessor.Process(tx1);

		hdPubKey = transactionProcessor.NewKey("B");
		var tx2 = CreateSpendingTransaction(result.ReceivedCoins[0].Coin, hdPubKey.P2wpkhScript);
		transactionProcessor.Process(tx2);

		var coins = transactionProcessor.Coins;
		Assert.Equal(coins.First().HdPubKey.Cluster, coins.Last().HdPubKey.Cluster);
		Assert.Equal("A, B", coins.First().HdPubKey.Cluster.Labels.ToString());
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

		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var hdPubKey = transactionProcessor.NewKey("A");
		var tx0 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx0);

		hdPubKey = transactionProcessor.NewKey("B");
		var tx1 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx1);

		hdPubKey = transactionProcessor.NewKey("C");
		var tx2 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx2);

		var smartCoinA = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "A");
		var smartCoinB = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "B");
		var smartCoinC = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "C");

		var myself = transactionProcessor.NewKey("D").P2wpkhScript;
		var changeScript = transactionProcessor.NewKey("").P2wpkhScript;
		var coins = new[] { smartCoinA.Coin, smartCoinB.Coin, smartCoinC.Coin };
		var tx3 = CreateSpendingTransaction(coins, myself, changeScript, replaceable: true);
		transactionProcessor.Process(tx3);

		var paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
		Assert.Equal("A, B, C, D", paymentCoin.HdPubKey.Cluster.Labels);

		coins = new[] { smartCoinB.Coin, smartCoinC.Coin, smartCoinA.Coin };
		var tx4 = CreateSpendingTransaction(coins, myself, changeScript);
		transactionProcessor.Process(tx4);

		paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
		Assert.Equal("A, B, C, D", paymentCoin.HdPubKey.Cluster.Labels);
	}

	[Fact]
	public async Task UpdateClusterAfterReplacedByFeeWithNewCoinsAsync()
	{
		// --tx0---> (A) --+
		//                 |
		// --tx1---> (B) --+---tx3 (replaceable)---> (pay to D - myself - cluster (D, A, B, C))
		//                 |    |
		// --tx2---> (C) --+    +----tx5 (replacement)---> (pay to D - coins is different order - cluster (D, A, B, C, X))
		//                      |
		// --tx4---> (X) -------+

		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var hdPubKey = transactionProcessor.NewKey("A");
		var tx0 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx0);

		hdPubKey = transactionProcessor.NewKey("B");
		var tx1 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx1);

		hdPubKey = transactionProcessor.NewKey("C");
		var tx2 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx2);

		var smartCoinA = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "A");
		var smartCoinB = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "B");
		var smartCoinC = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "C");

		var myself = transactionProcessor.NewKey("D").P2wpkhScript;
		var changeScript = transactionProcessor.NewKey("").P2wpkhScript;
		var coins = new[] { smartCoinA.Coin, smartCoinB.Coin, smartCoinC.Coin };
		var tx3 = CreateSpendingTransaction(coins, myself, changeScript, replaceable: true);
		transactionProcessor.Process(tx3);

		var paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
		Assert.Equal("A, B, C, D", paymentCoin.HdPubKey.Cluster.Labels);

		hdPubKey = transactionProcessor.NewKey("X");
		var tx4 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx4);
		var smartCoinX = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "X");

		coins = new[] { smartCoinB.Coin, smartCoinX.Coin, smartCoinC.Coin, smartCoinA.Coin };
		var tx5 = CreateSpendingTransaction(coins, myself, changeScript);
		transactionProcessor.Process(tx5);

		paymentCoin = Assert.Single(transactionProcessor.Coins, c => c.ScriptPubKey == myself);
		Assert.Equal("A, B, C, D, X", paymentCoin.HdPubKey.Cluster.Labels);
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

		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var hdPubKey = transactionProcessor.NewKey("A");
		var tx0 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m), height: 54321);
		transactionProcessor.Process(tx0);

		hdPubKey = transactionProcessor.NewKey("B");
		var tx1 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m), height: 54322);
		transactionProcessor.Process(tx1);

		hdPubKey = transactionProcessor.NewKey("C");
		var tx2 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m), height: 54323);
		transactionProcessor.Process(tx2);

		var smartCoinA = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "A");
		var smartCoinB = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "B");
		var smartCoinC = Assert.Single(transactionProcessor.Coins, c => c.HdPubKey.Cluster.Labels == "C");

		var changeScript = transactionProcessor.NewKey("D").P2wpkhScript;
		var coins = new[] { smartCoinA.Coin, smartCoinB.Coin, smartCoinC.Coin };
		using Key key = new();
		var tx3 = CreateSpendingTransaction(coins, key.PubKey.ScriptPubKey, changeScript, height: 55555);
		transactionProcessor.Process(tx3);

		var changeCoinD = Assert.Single(transactionProcessor.Coins);
		Assert.Equal("A, B, C, D", changeCoinD.HdPubKey.Cluster.Labels);
		Assert.Equal(smartCoinA.HdPubKey.Cluster, changeCoinD.HdPubKey.Cluster);
		Assert.Equal(smartCoinB.HdPubKey.Cluster, changeCoinD.HdPubKey.Cluster);
		Assert.Equal(smartCoinC.HdPubKey.Cluster, changeCoinD.HdPubKey.Cluster);

		// reorg
		Assert.True(changeCoinD.Confirmed);
		transactionProcessor.UndoBlock(tx3.Height);
		Assert.False(changeCoinD.Confirmed);

		Assert.Equal("A, B, C, D", changeCoinD.HdPubKey.Cluster.Labels);
		Assert.Equal(smartCoinA.HdPubKey.Cluster, changeCoinD.HdPubKey.Cluster);
		Assert.Equal(smartCoinB.HdPubKey.Cluster, changeCoinD.HdPubKey.Cluster);
		Assert.Equal(smartCoinC.HdPubKey.Cluster, changeCoinD.HdPubKey.Cluster);
	}

	[Fact]
	public async Task EnoughAnonymitySetClusteringAsync()
	{
		// --tx0---> (A) --tx1---> (empty)
		//
		// Note: tx1 is a coinjoin transaction

		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		var hdPubKey = transactionProcessor.NewKey("A");
		var tx0 = CreateCreditingTransaction(hdPubKey.P2wpkhScript, Money.Coins(1.0m));
		transactionProcessor.Process(tx0);

		var receivedCoin = Assert.Single(transactionProcessor.Coins);

		// build coinjoin transaction
		var coinjoinTransaction = Network.RegTest.CreateTransaction();

		for (var i = 0; i < 100; i++)
		{
			coinjoinTransaction.Inputs.Add(GetRandomOutPoint(), Script.Empty, WitScript.Empty);
		}
		for (var i = 0; i < 100; i++)
		{
			coinjoinTransaction.Outputs.Add(Money.Coins(0.1m), new Key());
		}
		coinjoinTransaction.Inputs.Add(receivedCoin.Outpoint, Script.Empty, WitScript.Empty);
		coinjoinTransaction.Outputs.Add(Money.Coins(0.1m), transactionProcessor.NewKey("").P2wpkhScript);
		coinjoinTransaction.Outputs.Add(Money.Coins(0.9m), transactionProcessor.NewKey("").P2wpkhScript);
		var tx1 = new SmartTransaction(coinjoinTransaction, Height.Mempool);

		transactionProcessor.Process(tx1);
		var anonymousCoin = Assert.Single(transactionProcessor.Coins, c => c.Amount == Money.Coins(0.1m));
		var changeCoin = Assert.Single(transactionProcessor.Coins, c => c.Amount == Money.Coins(0.9m));

		Assert.Empty(anonymousCoin.HdPubKey.Cluster.Labels);
		Assert.NotEmpty(changeCoin.HdPubKey.Cluster.Labels);
	}

	[Fact]
	public async Task GetPocketsAsync()
	{
		int targetAnonSet = 60;
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("A").P2wpkhScript, Money.Coins(1.0m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("A").P2wpkhScript, Money.Coins(1.0m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("a").P2wpkhScript, Money.Coins(1.0m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("B").P2wpkhScript, Money.Coins(1.0m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("C").P2wpkhScript, Money.Coins(1.0m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("C").P2wpkhScript, Money.Coins(1.0m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("A, B").P2wpkhScript, Money.Coins(1.0m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("").P2wpkhScript, Money.Coins(1.0m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("").P2wpkhScript, Money.Coins(1.0m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("").P2wpkhScript, Money.Coins(1.0m)));

		var notYetPrivateCoin = transactionProcessor.NewKey("");
		transactionProcessor.Process(CreateCreditingTransaction(notYetPrivateCoin.P2wpkhScript, Money.Coins(1.0m)));
		notYetPrivateCoin.SetAnonymitySet(targetAnonSet - 1, 0);

		var privateCoin1 = transactionProcessor.NewKey("");
		transactionProcessor.Process(CreateCreditingTransaction(privateCoin1.P2wpkhScript, Money.Coins(1.0m)));
		privateCoin1.SetAnonymitySet(targetAnonSet, 0);

		var privateCoin2 = transactionProcessor.NewKey("");
		transactionProcessor.Process(CreateCreditingTransaction(privateCoin2.P2wpkhScript, Money.Coins(1.0m)));
		privateCoin2.SetAnonymitySet(targetAnonSet, 0);

		var pockets = CoinPocketHelper.GetPockets(transactionProcessor.Coins, targetAnonSet);
		var aPocket = pockets.Single(x => x.Labels == "A");

		Assert.Equal(3, aPocket.Coins.Count());
		Assert.Equal(Money.Coins(3.0m), aPocket.Coins.TotalAmount());
		Assert.Single(pockets.Single(x => x.Labels == "B").Coins);
		Assert.Equal(2, pockets.Single(x => x.Labels == "C").Coins.Count());
		Assert.Single(pockets.Single(x => x.Labels == "A, B").Coins);
		Assert.Single(pockets.Single(x => x.Labels == CoinPocketHelper.SemiPrivateFundsText).Coins);
		Assert.Equal(3, pockets.Single(x => x.Labels == CoinPocketHelper.UnlabelledFundsText).Coins.Count());
		Assert.Equal(2, pockets.Single(x => x.Labels == CoinPocketHelper.PrivateFundsText).Coins.Count());
	}

	// Repro of issue 11101 https://github.com/zkSNACKs/WalletWasabi/issues/11101
	[Fact]
	public async Task GetPocketsShouldReturnExpectedPocketListAsync()
	{
		// ARRANGE
		int targetAnonSet = 10;
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("").P2wpkhScript, Money.Coins(0.0000_1000m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("Faucet").P2wpkhScript, Money.Coins(0.0000_1000m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("Electrum").P2wpkhScript, Money.Coins(0.0000_0670m)));

		// ACT
		var pockets = CoinPocketHelper.GetPockets(transactionProcessor.Coins, targetAnonSet);

		// ASSERT
		var expectedPockets = new LabelsArray[]
		{
			CoinPocketHelper.UnlabelledFundsText,
			"Faucet",
			"Electrum"
		}.ToHashSet(LabelsComparer.Instance);
		var actualPockets = pockets.Select(tuple => tuple.Labels).ToHashSet(LabelsComparer.Instance);

		Assert.True(expectedPockets.SetEquals(actualPockets));
	}

	[Fact]
	public async Task GetPocketsShouldBeCaseInsensitiveAsync()
	{
		// ARRANGE
		int targetAnonSet = 10;
		await using var txStore = await CreateTransactionStoreAsync();
		var transactionProcessor = CreateTransactionProcessor(txStore);
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("Pocket").P2wpkhScript, Money.Coins(0.0000_1000m)));
		transactionProcessor.Process(CreateCreditingTransaction(transactionProcessor.NewKey("PoCKeT").P2wpkhScript, Money.Coins(0.0000_0670m)));

		// ACT
		var pockets = CoinPocketHelper.GetPockets(transactionProcessor.Coins, targetAnonSet);

		// ASSERT
		var expectedPockets = new LabelsArray[] { "Pocket", }.ToHashSet(comparer: LabelsComparer.Instance);
		var actualPockets = pockets.Select(tuple => tuple.Labels).ToHashSet(comparer: LabelsComparer.Instance);

		Assert.True(expectedPockets.SetEquals(actualPockets));
	}

	private static SmartTransaction CreateSpendingTransaction(Coin coin, Script? scriptPubKey = null, int height = 0)
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

	private async Task<AllTransactionStore> CreateTransactionStoreAsync([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
	{
		string dir = Path.Combine(Common.GetWorkDir(callerFilePath, callerMemberName), "TransactionStore");
		await IoHelpers.TryDeleteDirectoryAsync(dir);
		AllTransactionStore txStore = new(dir, Network.RegTest);
		await txStore.InitializeAsync();
		return txStore;
	}

	private TransactionProcessor CreateTransactionProcessor(AllTransactionStore transactionStore)
	{
		var keyManager = KeyManager.CreateNew(out _, "password", Network.Main);

		return new TransactionProcessor(
			transactionStore,
			null,
			keyManager,
			Money.Coins(0.0001m));
	}

	private List<TxOut> CreateRandomIndistinguishableOutputs(long amount, int count)
	{
		List<TxOut> outputs = new(capacity: count);

		for (int i = 0; i < count; i++)
		{
			outputs.Add(new TxOut(amount, BitcoinFactory.CreateScript()));
		}

		return outputs;
	}

	private List<TxIn> CreateRandomInputs(int count)
	{
		List<TxIn> inputs = new(capacity: count);

		for (int i = 0; i < count; i++)
		{
			inputs.Add(new TxIn(GetRandomOutPoint(), Script.Empty));
		}

		return inputs;
	}
}
