using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.P2p;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// The purpose of this class is to safely and performantly manage all the Bitcoin related data
	/// that's being serialized to disk, like transactions, wallet files, keys, blocks, index files, etc...
	/// </summary>
	public class BitcoinStore
	{
		public BitcoinStore(
			string workFolderPath,
			Network network,
			IndexStore indexStore,
			AllTransactionStore transactionStore,
			MempoolService mempoolService)
		{
			var workFolderPath2 = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			IoHelpers.EnsureDirectoryExists(workFolderPath2);

			Guard.NotNull(nameof(network), network);
			NetworkWorkFolderPath = Path.Combine(workFolderPath2, network.ToString());
			IndexStoreFolderPath = Path.Combine(NetworkWorkFolderPath, "IndexStore");

			IndexStore = indexStore;
			TransactionStore = transactionStore;
			MempoolService = mempoolService;
		}

		/// <summary>
		/// Special constructor used by the mock version.
		/// </summary>
		internal BitcoinStore()
		{
			TransactionStore = new AllTransactionStoreMock();
		}

		public bool IsInitialized { get; private set; }
		private string NetworkWorkFolderPath { get; }
		private string IndexStoreFolderPath { get; }

		public IndexStore IndexStore { get; }
		public AllTransactionStore TransactionStore { get; }
		public SmartHeaderChain SmartHeaderChain => IndexStore.SmartHeaderChain;
		public MempoolService MempoolService { get; }

		/// <summary>
		/// This should not be a property, but a creator function, because it'll be cloned left and right by NBitcoin later.
		/// So it should not be assumed it's some singleton.
		/// </summary>
		public UntrustedP2pBehavior CreateUntrustedP2pBehavior() => new UntrustedP2pBehavior(MempoolService);

		public async Task InitializeAsync()
		{
			using (BenchmarkLogger.Measure())
			{
				var initTasks = new[]
				{
					IndexStore.InitializeAsync(IndexStoreFolderPath),
					TransactionStore.InitializeAsync(NetworkWorkFolderPath)
				};

				await Task.WhenAll(initTasks).ConfigureAwait(false);

				IsInitialized = true;
			}
		}
	}
}
