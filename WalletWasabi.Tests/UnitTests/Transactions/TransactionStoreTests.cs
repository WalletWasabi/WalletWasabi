using NBitcoin;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

public class TransactionStoreTests
{
	[Fact]
	public async Task CanInitializeAsync()
	{
		await using var txStore = await CreateTransactionStoreAsync();

		Assert.Equal(Network.Main, txStore.Network);
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
		await using var txStore = await CreateTransactionStoreAsync();

		Assert.True(txStore.IsEmpty());

		var stx = BitcoinFactory.CreateSmartTransaction();
		var operation = txStore.TryAddOrUpdate(stx);
		Assert.True(operation.isAdded);
		Assert.False(operation.isUpdated);
		var isRemoved = txStore.TryRemove(stx.GetHash(), out var removed);
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
				stx.IsSpeedup,
				stx.IsCancellation,
				stx.FirstSeen));
		Assert.False(operation.isAdded);
		Assert.True(operation.isUpdated);

		operation = txStore.TryAddOrUpdate(stx);
		Assert.False(operation.isAdded);
		Assert.False(operation.isUpdated);

		Assert.False(txStore.IsEmpty());
		Assert.True(txStore.TryGetTransaction(stx.GetHash(), out var sameStx));
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

	private static async Task<TransactionStore> CreateTransactionStoreAsync([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
	{
		// Make sure starts with clear state.
		var dir = Path.Combine(Common.GetWorkDir(callerFilePath, callerMemberName));
		var filePath = Path.Combine(dir, "Transactions.dat");
		if (File.Exists(filePath))
		{
			File.Delete(filePath);
		}
		var txStore = new TransactionStore(dir, Network.Main);
		await txStore.InitializeAsync($"{nameof(TransactionStore)}.{nameof(TransactionStore.InitializeAsync)}", CancellationToken.None);
		return txStore;
	}
}
