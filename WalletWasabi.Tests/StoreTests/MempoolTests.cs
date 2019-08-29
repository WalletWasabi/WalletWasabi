using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Models;
using WalletWasabi.Stores.Mempool;
using Xunit;

namespace WalletWasabi.Tests.StoreTests
{
	public class MempoolTests
	{
		[Fact]
		public async Task CanInitializeMempoolStoreAsync()
		{
			var mempoolStore = new MempoolStore();

			var dir = Path.Combine(Global.Instance.DataDir, nameof(CanInitializeMempoolStoreAsync));
			var network = Network.Main;
			await mempoolStore.InitializeAsync(dir, network);

			Assert.Equal(network, mempoolStore.Network);
			Assert.Equal(dir, mempoolStore.WorkFolderPath);
		}

		[Fact]
		public async Task CanDoOperationsAsync()
		{
			var mempoolStore = new MempoolStore();

			var dir = Path.Combine(Global.Instance.DataDir, nameof(CanDoOperationsAsync));
			Directory.Delete(dir, recursive: true);
			var network = Network.Main;
			await mempoolStore.InitializeAsync(dir, network);

			Assert.True(mempoolStore.IsEmpty());

			var tx = Transaction.Create(network);
			var stx = new SmartTransaction(tx, Height.Mempool, firstSeenIfMempoolTime: DateTimeOffset.UtcNow);
			Assert.True(mempoolStore.TryAdd(stx.GetHash()));
			var isAdded = mempoolStore.TryAdd(stx);
			Assert.False(isAdded.isHashAdded);
			Assert.True(isAdded.isTxAdded);
			var isRemoved = mempoolStore.TryRemove(stx.GetHash(), out SmartTransaction removed);
			Assert.True(isRemoved.isHashRemoved);
			Assert.True(isRemoved.isTxRemoved);
			Assert.Equal(stx, removed);
			isAdded = mempoolStore.TryAdd(stx);
			Assert.True(isAdded.isHashAdded);
			Assert.True(isAdded.isTxAdded);
			isAdded = mempoolStore.TryAdd(stx);
			Assert.False(isAdded.isHashAdded);
			Assert.False(isAdded.isTxAdded);
			Assert.False(mempoolStore.TryAdd(stx.GetHash()));
			Assert.False(mempoolStore.IsEmpty());
			Assert.True(mempoolStore.TryGetTransaction(tx.GetHash(), out SmartTransaction sameStx));
			Assert.True(mempoolStore.ContainsHash(tx.GetHash()));
			Assert.Equal(stx, sameStx);

			var tx2Hash = uint256.One;
			Assert.True(mempoolStore.TryAdd(tx2Hash));
			Assert.False(mempoolStore.TryAdd(tx2Hash));
			Assert.False(mempoolStore.IsEmpty());
			Assert.False(mempoolStore.TryGetTransaction(tx2Hash, out _));
			Assert.True(mempoolStore.ContainsHash(tx2Hash));

			mempoolStore.TryRemove(tx.GetHash(), out _);
			Assert.False(mempoolStore.IsEmpty());
			Assert.Empty(mempoolStore.GetTransactions());
			var txHashes = mempoolStore.GetTransactionHashes();
			Assert.Single(txHashes);
			Assert.Equal(tx2Hash, txHashes.Single());
			mempoolStore.TryAdd(stx);

			var removedCount = mempoolStore.RemoveExcept(new string[] { tx2Hash.ToString().Substring(0, 10), tx.GetHash().ToString().Substring(0, 10) }.ToHashSet(), 10).Count;
			Assert.Equal(0, removedCount);
			Assert.Equal(1, mempoolStore.GetTransactions().Count);
			Assert.Equal(2, mempoolStore.GetTransactionHashes().Count);

			removedCount = mempoolStore.RemoveExcept(new string[] { tx.GetHash().ToString().Substring(0, 10) }.ToHashSet(), 10).Count;
			Assert.Equal(1, removedCount);
			Assert.Equal(1, mempoolStore.GetTransactions().Count);
			Assert.Equal(1, mempoolStore.GetTransactionHashes().Count);

			mempoolStore.TryAdd(tx2Hash);
			removedCount = mempoolStore.RemoveExcept(new string[] { "foo" }.ToHashSet(), 3).Count;
			Assert.Equal(2, removedCount);
			Assert.Equal(0, mempoolStore.GetTransactions().Count);
			Assert.Equal(0, mempoolStore.GetTransactionHashes().Count);

			mempoolStore.TryAdd(tx2Hash);
			mempoolStore.TryAdd(stx);
			removedCount = mempoolStore.RemoveExcept(new string[] { tx2Hash.ToString().Substring(0, 10) }.ToHashSet(), 10).Count;
			Assert.Equal(1, removedCount);
			Assert.Equal(0, mempoolStore.GetTransactions().Count);
			Assert.Equal(1, mempoolStore.GetTransactionHashes().Count);
		}
	}
}
