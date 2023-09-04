using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Stores;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

/// <summary>
/// Tests for <see cref="TransactionSqliteStorage"/>
/// </summary>
public class TransactionSqliteStorageTests
{
	[Theory]
	[InlineData("main")]
	[InlineData("testnet")]
	public void InsertDelete(string networkString)
	{
		Network? network = Network.GetNetwork(networkString);
		Assert.NotNull(network);

		using TransactionSqliteStorage storage = TransactionSqliteStorage.FromFile(dataSource: SqliteStorageHelper.InMemoryDatabase, network);

		SmartTransaction tx1 = SmartTransaction.FromLine("34fc45781f2ac8e541b6045c2858c755dd2ab85e0ea7b5778b4d0cc191468571:01000000000102d5ae6e2612cdf8932d0e4f684d8ad9afdbca0afffba5c3dc0bf85f2b661bfb670000000000ffffffffbfd117908d5ba536624630519aaea7419605efa33bf1cb50c5ff7441f7b27a5b0100000000ffffffff01c6473d0000000000160014f9d25fe27812c3d35ad3819fcab8b95098effe15024730440220165730f8275434a5484b6aba3c71338a109b7cfd7380fdc18c6791a6afba9dee0220633b3b65313e57bdbd491d17232e6702912869d81461b4c939600d1cc99c06ec012102667c9fb272660cd6c06f853314b53c41da851f86024f9475ff15ea0636f564130247304402205e81562139231274cd7f705772c2810e34b7a291bd9882e6c567553babca9c7402206554ebd60d62a605a27bd4bf6482a533dd8dd35b12dc9d6abfa21738fdf7b57a012102b25dec5439a1aad8d901953a90d16252514a55f547fe2b85bc12e3f72cff1c4b00000000:Mempool::0::1570464578:False", network);
		SmartTransaction tx2 = SmartTransaction.FromLine("b5cd5b4431891d913a6fbc0e946798b6f730c8b97f95a5c730c24189bed24ce7:01000000010113145dd6264da43406a0819b6e2d791ec9281322f33944ef034c3307f38c330000000000ffffffff0220a10700000000001600149af662cf9564700b93bd46fac8b51f64f2adad2343a5070000000000160014f1938902267eac1ae527128fe7773657d2b757b900000000:Mempool::0::1555590391:False", network);

		Assert.True(storage.IsEmpty());
		Assert.False(storage.Contains(tx1.GetHash()));

		// Insert 2 transactions.
		int added = storage.BulkInsert(new SmartTransaction[] { tx1, tx2 });
		Assert.Equal(2, added);

		// Attempt to insert tx1 again. Should fail because the same txid is already present.
		added = storage.BulkInsert(new SmartTransaction[] { tx1 });
		Assert.Equal(0, added);

		Assert.False(storage.Contains(uint256.Zero));
		Assert.False(storage.Contains(uint256.One));
		Assert.True(storage.Contains(tx1.GetHash()));
		Assert.True(storage.Contains(tx2.GetHash()));

		// Retrieve tx1 to test that the tx1 is serialized and deserialized correctly.
		Assert.True(storage.TryGet(tx1.GetHash(), out SmartTransaction? txActual));
		Assert.NotNull(txActual);
		Assert.NotSame(tx1, txActual); // A new instance was created in the process.

		// Make sure that tx1 was deserialized correctly.
		Assert.Equal(tx1.GetHash(), txActual.GetHash());
		Assert.Equal(tx1.Transaction.ToBytes(), txActual.Transaction.ToBytes());
		Assert.Equal(tx1.Height, txActual.Height);
		Assert.Equal(tx1.BlockHash, txActual.BlockHash);
		Assert.Equal(tx1.BlockIndex, txActual.BlockIndex);
		Assert.Equal(tx1.FirstSeen, txActual.FirstSeen);
		Assert.Equal(tx1.Labels, txActual.Labels);
		Assert.Equal(tx1.IsReplacement, txActual.IsReplacement);
		Assert.Equal(tx1.IsSpeedup, txActual.IsSpeedup);
		Assert.Equal(tx1.IsCancellation, txActual.IsCancellation);

		Assert.False(storage.IsEmpty());

		// Remove tx1 so that only tx2 still remains in the storage.
		Assert.Equal(1, storage.BulkRemove(new uint256[] { tx1.GetHash() }));
		Assert.False(storage.Contains(tx1.GetHash()));
		Assert.True(storage.Contains(tx2.GetHash()));

		// Attempt to delete tx1 again. Should fail because we've just done it.
		Assert.Equal(0, storage.BulkRemove(new uint256[] { tx1.GetHash() }));

		// Test table clearing.
		Assert.True(storage.Clear());
		Assert.True(storage.IsEmpty());
	}
}
