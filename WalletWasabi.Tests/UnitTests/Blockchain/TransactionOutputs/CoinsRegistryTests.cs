using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.TransactionOutputs;

/// <summary>
/// Tests for <see cref="CoinsRegistry"/> class.
/// </summary>
public class CoinsRegistryTests
{
	public CoinsRegistryTests()
	{
		KeyManager = KeyManager.CreateNew(out _, password: "password", Network.Main);
	}

	private KeyManager KeyManager { get; }
	private CoinsRegistry Coins { get; } = new();

	/// <summary>
	/// Tests <see cref="CoinsRegistry.Undo(uint256)"/> method.
	/// </summary>
	[Fact]
	public void UndoTransaction()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromSeconds(30));

		// --tx0---> (A) --tx1 (replaceable)-+--> (B) --tx2---> (D)
		//                  |                |
		//                  |                +--> (C)
		//                  |
		//                  +--tx3 (replacement)---> (E)

		Coin? tx0Coin;
		SmartTransaction tx0; // The transaction has 2 inputs and 1 output.
		SmartTransaction tx1; // The transaction has 1 input and 2 outputs.
		SmartTransaction tx2; // The transaction has 1 input and 1 output.
		SmartTransaction tx3; // The transaction has 1 input and 1 output.

		// Create and process transaction tx0.
		{
			tx0 = CreateCreditingTransaction(NewInternalKey(label: "A").P2wpkhScript, Money.Coins(1.0m), height: 54321);
			tx0Coin = tx0.Transaction.Outputs.AsCoins().First();

			IReadOnlyList<SmartCoin> tx0Coins = ProcessTransaction(tx0);
			Assert.Single(tx0Coins);
			Assert.Single(Coins);
			Assert.Equal(tx0Coins[0], Coins.First());

			// Verify that both tx0 inputs' prevOuts are set to be spent by the single tx0's coin.
			foreach (var input in tx0.Transaction.Inputs)
			{
				Assert.True(Coins.TryGetCoinsByInputPrevOut(input.PrevOut, out var coinsSpendingTx0PrevOuts));
				Assert.Single(coinsSpendingTx0PrevOuts);
			}
		}

		// Create and process transaction tx1 that fully spends tx0.
		{
			tx1 = CreateSpendingTransaction(tx0Coin, txOut: new TxOut(Money.Coins(0.85m), NewInternalKey(label: "B").P2wpkhScript));
			tx1.Transaction.Inputs[0].Sequence = Sequence.OptInRBF;
			tx1.Transaction.Outputs.Add(Money.Coins(0.1m), NewInternalKey("C").P2wpkhScript);

			IReadOnlyList<SmartCoin> tx1Coins = ProcessTransaction(tx1);
			AssertEqualCoinSets(Coins, tx1Coins);

			SmartCoin unconfirmedCoin1 = Assert.Single(Coins, coin => coin.HdPubKey.Labels == "B");
			SmartCoin unconfirmedCoin2 = Assert.Single(Coins, coin => coin.HdPubKey.Labels == "C");
			Assert.True(unconfirmedCoin1.IsReplaceable());
			Assert.True(unconfirmedCoin2.IsReplaceable());

			Assert.True(Coins.IsKnown(tx0.GetHash()));
			Assert.True(Coins.IsKnown(tx1.GetHash()));

			// Verify that we cache properly the coins spending inputs' prevOuts of tx0 and tx1.
			{
				// Check both tx0 inputs.
				foreach (var input in tx0.Transaction.Inputs)
				{
					Assert.True(Coins.TryGetCoinsByInputPrevOut(input.PrevOut, out var coinsSpendingTx0PrevOuts));
					Assert.Single(coinsSpendingTx0PrevOuts);
				}

				// Check the input of tx1. Both tx1's outputs should be found.
				TxIn tx1Input = Assert.Single(tx1.Transaction.Inputs);
				Assert.True(Coins.TryGetCoinsByInputPrevOut(tx1Input.PrevOut, out var coinsSpendingTx1PrevOut));
				Assert.Equal(2, coinsSpendingTx1PrevOut.Count);
			}
		}

		// Create and process transaction tx2 that partially spends tx1.
		{
			SmartCoin coin = Assert.Single(Coins, coin => coin.HdPubKey.Labels == "B");

			tx2 = CreateSpendingTransaction(coin.Coin, txOut: new TxOut(Money.Coins(0.7m), NewInternalKey("D").P2wpkhScript));

			IReadOnlyList<SmartCoin> tx2Coins = ProcessTransaction(tx2);
			Assert.Single(tx2Coins);

			Assert.True(Coins.IsKnown(tx1.GetHash()));
			Assert.True(Coins.IsKnown(tx2.GetHash()));

			// Verify that we cache properly the coins spending inputs' prevOuts of tx0, tx1 and tx2.
			{
				// No change for tx0 inputs is expected.
				foreach (TxIn input in tx0.Transaction.Inputs)
				{
					Assert.True(Coins.TryGetCoinsByInputPrevOut(input.PrevOut, out var coinsSpendingTx0PrevOuts));
					Assert.Single(coinsSpendingTx0PrevOuts);
				}

				// No change for the tx1's input is expected.
				TxIn tx1Input = Assert.Single(tx1.Transaction.Inputs);
				Assert.True(Coins.TryGetCoinsByInputPrevOut(tx1Input.PrevOut, out var coinsSpendingTx1PrevOut));
				Assert.Equal(2, coinsSpendingTx1PrevOut.Count);

				// Input of tx2 must be processed correctly.
				TxIn tx2Input = Assert.Single(tx2.Transaction.Inputs);
				Assert.True(Coins.TryGetCoinsByInputPrevOut(tx2Input.PrevOut, out var coinsSpendingTx2PrevOut));
				Assert.Single(coinsSpendingTx2PrevOut);
			}
		}

		// Create and process REPLACEMENT transaction tx3 that fully spends tx0.
		{
			// Undo tx1.
			{
				Assert.True(Coins.IsKnown(tx0.GetHash()));
				Assert.True(Coins.IsKnown(tx1.GetHash()));
				Assert.True(Coins.IsKnown(tx2.GetHash()));

				// First undo tx1 which should transitively undo tx2.
				(ICoinsView toRemove, ICoinsView toAdd) = Coins.Undo(tx1.GetHash());

				Assert.True(Coins.IsKnown(tx0.GetHash()));
				Assert.False(Coins.IsKnown(tx1.GetHash()));
				Assert.False(Coins.IsKnown(tx2.GetHash()));

				foreach (var input in tx0.Transaction.Inputs)
				{
					Assert.True(Coins.TryGetCoinsByInputPrevOut(input.PrevOut, out var coinsSpendingPrevOut));
					Assert.Single(coinsSpendingPrevOut);
				}

				TxIn tx1Input = Assert.Single(tx1.Transaction.Inputs);
				Assert.False(Coins.TryGetCoinsByInputPrevOut(tx1Input.PrevOut, out var coinsSpendingTx1PrevOut));
				Assert.Null(coinsSpendingTx1PrevOut);

				TxIn tx2Input = Assert.Single(tx2.Transaction.Inputs);
				Assert.False(Coins.TryGetCoinsByInputPrevOut(tx2Input.PrevOut, out var coinsSpendingTx2PrevOut));
				Assert.Null(coinsSpendingTx2PrevOut);
			}

			// .. then create and process tx3.
			{
				tx3 = CreateSpendingTransaction(tx0Coin, txOut: new TxOut(Money.Coins(0.9m), NewInternalKey("E").P2wpkhScript));
				IReadOnlyList<SmartCoin> tx3Coins = ProcessTransaction(tx3);

				SmartCoin finalCoin = Assert.Single(Coins);
				Assert.Equal("E", finalCoin.HdPubKey.Labels);

				Assert.Empty(Coins.AsAllCoinsView().Where(coin => coin.HdPubKey.Labels == "B"));
				Assert.Empty(Coins.AsAllCoinsView().Where(coin => coin.HdPubKey.Labels == "C"));
				Assert.Empty(Coins.AsAllCoinsView().Where(coin => coin.HdPubKey.Labels == "D"));

				// Replaced transactions tx1 and tx2 have to be removed because tx3 replaced tx1.
				Assert.False(Coins.IsKnown(tx1.GetHash()));
				Assert.False(Coins.IsKnown(tx2.GetHash()));
				Assert.True(Coins.IsKnown(tx3.GetHash()));

				TxIn tx3Input = Assert.Single(tx3.Transaction.Inputs);
				Assert.True(Coins.TryGetCoinsByInputPrevOut(tx3Input.PrevOut, out var coinsSpendingTx3PrevOut));
				Assert.Single(coinsSpendingTx3PrevOut);
			}
		}
	}

	/// <summary>Modify UTXO set in <see cref="CoinsRegistry"/> with <paramref name="tx">transaction</paramref> in mind.</summary>
	private IReadOnlyList<SmartCoin> ProcessTransaction(SmartTransaction tx)
	{
		List<SmartCoin> result = new(capacity: tx.Transaction.Outputs.Count);

		// Add new coins to the registry.
		foreach (Coin coin in tx.Transaction.Outputs.AsCoins())
		{
			if (KeyManager.TryGetKeyForScriptPubKey(coin.ScriptPubKey, out HdPubKey? pubKey))
			{
				SmartCoin newCoin = new(tx, outputIndex: coin.Outpoint.N, pubKey: pubKey);
				Assert.True(Coins.TryAdd(newCoin));
				result.Add(newCoin);
			}
		}

		// Remove spent coins from the registry.
		IReadOnlyList<SmartCoin> myInputs = Coins.GetMyInputs(tx);

		foreach (SmartCoin spentCoin in myInputs)
		{
			Coins.Spend(spentCoin, tx);
		}

		return result;
	}

	private static SmartTransaction CreateCreditingTransaction(Script scriptPubKey, Money amount, int height)
	{
		Transaction tx = Network.Main.CreateTransaction();
		tx.Version = 1;
		tx.LockTime = LockTime.Zero;
		tx.Inputs.Add(GetRandomOutPoint(), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
		tx.Inputs.Add(GetRandomOutPoint(), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
		tx.Outputs.Add(amount, scriptPubKey);

		return new SmartTransaction(tx, height == 0 ? Height.Mempool : new Height(height));
	}

	/// <summary>Creates a transaction that fully spends the given coin to a single outpoint (leaving rest for fees).</summary>
	private static SmartTransaction CreateSpendingTransaction(Coin coin, TxOut txOut, int height = 0)
	{
		var tx = Network.Main.CreateTransaction();
		tx.Inputs.Add(coin.Outpoint, Script.Empty, WitScript.Empty);
		tx.Outputs.Add(txOut);
		return new SmartTransaction(tx, height == 0 ? Height.Mempool : new Height(height));
	}

	private static OutPoint GetRandomOutPoint()
		=> new(RandomUtils.GetUInt256(), 0);

	private HdPubKey NewInternalKey(string label)
		=> KeyManager.GenerateNewKey(label, KeyState.Clean, isInternal: true);

	/// <summary>Compare coins registry and actual coins as two sets (disregarding ordering).</summary>
	private void AssertEqualCoinSets(CoinsRegistry coins, IEnumerable<SmartCoin> actualCoins)
		=> Assert.Equal(new HashSet<SmartCoin>(coins), new HashSet<SmartCoin>(actualCoins));
}
