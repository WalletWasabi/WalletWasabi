using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.BitcoinCore.Mempool;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

/// <summary>
/// Tests for <see cref="Mempool"/>.
/// </summary>
public class MempoolTests
{
	/// <summary>
	/// Verifies behavior for <see cref="Mempool.AddTransaction(Transaction)"/>.
	/// </summary>
	[Fact]
	public void AddRemoveTransaction()
	{
		// Create the tx1 transaction.
		Transaction tx1;
		OutPoint tx1prevOut1;
		{
			tx1 = Network.Main.CreateTransaction();
			tx1prevOut1 = new(hashIn: new uint256(0x1), nIn: 0);
			tx1.Inputs.Add(new TxIn(tx1prevOut1));

			OutPoint tx1prevOut2 = new(hashIn: new uint256(0x2), nIn: 0);
			tx1.Inputs.Add(new TxIn(tx1prevOut2));

			TxOut tx1Out1 = new(Money.Coins(0.1m), scriptPubKey: Script.Empty);
			tx1.Outputs.Add(tx1Out1);
		}

		// Create the tx2 transaction that spends the same tx1prevOut1 as tx1.
		Transaction tx2;
		{
			tx2 = Network.Main.CreateTransaction();
			tx2.Inputs.Add(new TxIn(tx1prevOut1));

			OutPoint tx2prevOut2 = new(hashIn: new uint256(0x3), nIn: 0);
			tx2.Inputs.Add(new TxIn(tx2prevOut2));

			TxOut tx2Out1 = new(Money.Coins(0.1m), scriptPubKey: Script.Empty);
			tx1.Outputs.Add(tx2Out1);
		}

		Mempool mempool = new();

		Assert.False(mempool.ContainsTransaction(tx1.GetHash()));

		mempool.AddTransaction(tx1);
		Assert.True(mempool.ContainsTransaction(tx1.GetHash()));

		// Tests that no exception is thrown.
		mempool.AddTransaction(tx1);
		Assert.True(mempool.ContainsTransaction(tx1.GetHash()));
		Assert.Equal(new HashSet<uint256>() { tx1.GetHash() }, mempool.GetMempoolTxids());

		// Now we expect that tx1 will be removed from the mempool because it spends
		// the same transaction output as tx2.
		mempool.AddTransaction(tx2);
		Assert.False(mempool.ContainsTransaction(tx1.GetHash()));
		Assert.True(mempool.ContainsTransaction(tx2.GetHash()));
		Assert.Equal(new HashSet<uint256>() { tx2.GetHash() }, mempool.GetMempoolTxids());

		// tx1 is not in the mempool anymore.
		Assert.False(mempool.TryRemoveTransaction(tx1.GetHash()));
		Assert.True(mempool.TryRemoveTransaction(tx2.GetHash()));

		// There are no transactions in the mempool now.
		Assert.Equal(Array.Empty<uint256>(), mempool.GetMempoolTxids());
	}

	/// <summary>
	/// Verifies that <see cref="Mempool.GetSpenderTransactions(IEnumerable{OutPoint})"/>
	/// returns correct distinct transactions from the mirrored mempool.
	/// </summary>
	[Fact]
	public void GetSpenderTransactions()
	{
		Mempool mempool = new();

		// Empty mirrored mempool & no transaction outputs.
		{
			IEnumerable<OutPoint> txOuts = Enumerable.Empty<OutPoint>();
			IEnumerable<Transaction> transactions = mempool.GetSpenderTransactions(txOuts);

			Assert.Empty(transactions);
		}

		// Empty mirrored mempool & a single transaction output.
		{
			OutPoint[] txOuts = new[] { new OutPoint(uint256.One, 1) };
			IEnumerable<Transaction> transactions = mempool.GetSpenderTransactions(txOuts);

			Assert.Empty(transactions);
		}

		Transaction tx1;
		OutPoint tx1prevOut1;
		OutPoint tx1prevOut2;
		TxOut tx1Out1;

		// Add a single transaction to the mirrored mempool.
		{
			tx1 = Network.Main.CreateTransaction();
			tx1prevOut1 = new(hashIn: new uint256(0x1), nIn: 0);
			tx1.Inputs.Add(new TxIn(tx1prevOut1));

			tx1prevOut2 = new(hashIn: new uint256(0x2), nIn: 0);
			tx1.Inputs.Add(new TxIn(tx1prevOut2));

			tx1Out1 = new TxOut(Money.Coins(0.1m), scriptPubKey: Script.Empty);
			tx1.Outputs.Add(tx1Out1);

			// Add the transaction.
			Assert.Equal(1, mempool.AddMissingTransactions(new Transaction[] { tx1 }));
		}

		// Mirrored mempool with tx1 & a non-existent prevOut.
		{
			OutPoint madeUpPrevOut = new(hashIn: new uint256(0x77777), nIn: 0);
			IReadOnlySet<Transaction> transactions = mempool.GetSpenderTransactions(new[] { madeUpPrevOut });

			Assert.Empty(transactions);
		}

		// Mirrored mempool with tx1 & existing tx1prevOut1.
		{
			IReadOnlySet<Transaction> transactions = mempool.GetSpenderTransactions(new[] { tx1prevOut1 });

			Transaction actualTx = Assert.Single(transactions);
			Assert.Equal(tx1, actualTx);
		}

		// Mirrored mempool with tx1 & existing tx1prevOut2.
		{
			IReadOnlySet<Transaction> transactions = mempool.GetSpenderTransactions(new[] { tx1prevOut2 });

			Transaction actualTx = Assert.Single(transactions);
			Assert.Equal(tx1, actualTx);
		}

		// Mirrored mempool with tx1 & existing tx1prevOut1 and tx1prevOut2.
		{
			IReadOnlySet<Transaction> transactions = mempool.GetSpenderTransactions(new[] { tx1prevOut1, tx1prevOut2 });

			Transaction actualTx = Assert.Single(transactions);
			Assert.Equal(tx1, actualTx);
		}
	}
}
