using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class TransactionStoreTests
	{
		[Fact]
		public async Task CanInitializeAsync()
		{
			var txStore = new TransactionStoreMock();
			var network = Network.Main;
			await txStore.InitializeAsync(network);

			Assert.Equal(network, txStore.Network);
			Assert.Empty(txStore.GetTransactionHashes());
			Assert.Empty(txStore.GetTransactions());
			Assert.True(txStore.IsEmpty());
			Assert.False(txStore.TryGetTransaction(uint256.One, out _));
			Assert.False(txStore.TryRemove(uint256.One, out _));
			Assert.NotEmpty(txStore.WorkFolderPath);
			Assert.True(File.Exists(Path.Combine(txStore.WorkFolderPath, "Transactions.dat")));
		}

		[Fact]
		public async Task CanDoOperationsAsync()
		{
			var txStore = new TransactionStoreMock();
			var network = Network.Main;
			await txStore.InitializeAsync(network);

			Assert.True(txStore.IsEmpty());

			var tx = Transaction.Create(network);
			var stx = new SmartTransaction(tx, Height.Mempool, firstSeen: DateTimeOffset.UtcNow);
			var isAdded = txStore.TryAdd(stx);
			Assert.True(isAdded);
			var isRemoved = txStore.TryRemove(stx.GetHash(), out SmartTransaction removed);
			Assert.True(isRemoved);
			Assert.Equal(stx, removed);
			isAdded = txStore.TryAdd(stx);
			Assert.True(isAdded);
			isAdded = txStore.TryAdd(stx);
			Assert.False(isAdded);
			Assert.False(txStore.TryAdd(stx));
			Assert.False(txStore.IsEmpty());
			Assert.True(txStore.TryGetTransaction(tx.GetHash(), out SmartTransaction sameStx));
			Assert.True(txStore.Contains(tx.GetHash()));
			Assert.Equal(stx, sameStx);

			txStore.TryRemove(tx.GetHash(), out _);
			Assert.True(txStore.IsEmpty());
			Assert.Empty(txStore.GetTransactions());
			txStore.TryAdd(stx);

			txStore.TryAdd(stx);
			Assert.Equal(1, txStore.GetTransactions().Count);
			Assert.Equal(1, txStore.GetTransactionHashes().Count);
		}
	}
}
