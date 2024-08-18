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

		mempool.AddTransaction(tx1.GetHash());
		Assert.True(mempool.ContainsTransaction(tx1.GetHash()));

		// Tests that no exception is thrown.
		mempool.AddTransaction(tx1.GetHash());
		Assert.True(mempool.ContainsTransaction(tx1.GetHash()));
		Assert.Equal(new HashSet<uint256>() { tx1.GetHash() }, mempool.GetMempoolTxids());

		// Both transactions are in the mempool, leading to an incorrect state.
		// This is because the Mempool doesn't have access to the transaction.
		// It must resynchronize the state later on with the Bitcoin node in a reasonable time.
		mempool.AddTransaction(tx2.GetHash());
		Assert.True(mempool.ContainsTransaction(tx1.GetHash()));
		Assert.True(mempool.ContainsTransaction(tx2.GetHash()));

		Assert.True(mempool.RemoveTransaction(tx1.GetHash()));
		Assert.True(mempool.RemoveTransaction(tx2.GetHash()));

		// There are no transactions in the mempool now.
		Assert.Equal(Array.Empty<uint256>(), mempool.GetMempoolTxids());
	}
}
