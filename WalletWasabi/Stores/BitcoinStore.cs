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
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			IoHelpers.EnsureDirectoryExists(WorkFolderPath);

			Network = Guard.NotNull(nameof(network), network);
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
		private string WorkFolderPath { get; }
		public Network Network { get; }

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
				var networkWorkFolderPath = Path.Combine(WorkFolderPath, Network.ToString());
				var indexStoreFolderPath = Path.Combine(networkWorkFolderPath, "IndexStore");

				var initTasks = new[]
				{
					IndexStore.InitializeAsync(indexStoreFolderPath),
					TransactionStore.InitializeAsync(networkWorkFolderPath)
				};

				await Task.WhenAll(initTasks).ConfigureAwait(false);

				IsInitialized = true;
			}
		}
	}
}
