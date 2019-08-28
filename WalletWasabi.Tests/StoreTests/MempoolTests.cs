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
		public async Task CanStoreTransactionsAsync()
		{
			var mempoolStore = new MempoolStore();

			var dir = Path.Combine(Global.Instance.DataDir, nameof(CanStoreTransactionsAsync));
			var network = Network.Main;
			await mempoolStore.InitializeAsync(dir, network);

			var tx = Transaction.Create(network);
			var stx = new SmartTransaction(tx, Height.Mempool, firstSeenIfMempoolTime: DateTimeOffset.UtcNow);
			mempoolStore.Add(stx);
			Assert.True(mempoolStore.TryGetTransaction(tx.GetHash(), out SmartTransaction sameStx));
			Assert.True(mempoolStore.ContainsHash(tx.GetHash()));
			Assert.Equal(stx, sameStx);

			var tx2Hash = uint256.One;
			mempoolStore.Add(tx2Hash);
			Assert.False(mempoolStore.TryGetTransaction(tx2Hash, out _));
			Assert.True(mempoolStore.ContainsHash(tx2Hash));

			mempoolStore.Remove(tx.GetHash());
			Assert.Empty(mempoolStore.GetTransactions());
			var txHashes = mempoolStore.GetTransactionHashes();
			Assert.Single(txHashes);
			Assert.Equal(tx2Hash, txHashes.Single());
		}
	}
}
