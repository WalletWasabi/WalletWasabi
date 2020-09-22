using NBitcoin;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
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

			var stx = Global.GenerateRandomSmartTransaction();
			var operation = txStore.TryAddOrUpdate(stx);
			Assert.True(operation.isAdded);
			Assert.False(operation.isUpdated);
			var isRemoved = txStore.TryRemove(stx.GetHash(), out SmartTransaction removed);
			Assert.True(isRemoved);
			Assert.Equal(stx, removed);
			operation = txStore.TryAddOrUpdate(stx);
			Assert.True(operation.isAdded);
			Assert.False(operation.isUpdated);
			operation = txStore.TryAddOrUpdate(stx);
			Assert.False(operation.isAdded);
			Assert.False(operation.isUpdated);

			operation = txStore.TryAddOrUpdate(
				new SmartTransaction(
					stx.Transaction,
					height: stx.Height,
					stx.BlockHash,
					stx.BlockIndex,
					"totally random new label",
					stx.IsReplacement,
					stx.FirstSeen));
			Assert.False(operation.isAdded);
			Assert.True(operation.isUpdated);

			operation = txStore.TryAddOrUpdate(stx);
			Assert.False(operation.isAdded);
			Assert.False(operation.isUpdated);

			Assert.False(txStore.IsEmpty());
			Assert.True(txStore.TryGetTransaction(stx.GetHash(), out SmartTransaction sameStx));
			Assert.True(txStore.Contains(stx.GetHash()));
			Assert.Equal(stx, sameStx);

			txStore.TryRemove(stx.GetHash(), out _);
			Assert.True(txStore.IsEmpty());
			Assert.Empty(txStore.GetTransactions());
			txStore.TryAddOrUpdate(stx);

			txStore.TryAddOrUpdate(stx);
			Assert.Single(txStore.GetTransactions());
			Assert.Single(txStore.GetTransactionHashes());
		}
	}
}
