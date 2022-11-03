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
