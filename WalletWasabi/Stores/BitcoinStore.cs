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
		public bool IsInitialized { get; private set; }
		private string WorkFolderPath { get; set; }
		public Network Network { get; private set; }

		public IndexStore IndexStore { get; private set; }
		public AllTransactionStore TransactionStore { get; private set; }
		public SmartHeaderChain HashChain { get; private set; }
		public MempoolService MempoolService { get; private set; }

		/// <summary>
		/// This should not be a property, but a creator function, because it'll be cloned left and right by NBitcoin later.
		/// So it should not be assumed it's some singleton.
		/// </summary>
		public UntrustedP2pBehavior CreateUntrustedP2pBehavior() => new UntrustedP2pBehavior(MempoolService);

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			using (BenchmarkLogger.Measure())
			{
				WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
				IoHelpers.EnsureDirectoryExists(WorkFolderPath);

				Network = Guard.NotNull(nameof(network), network);

				IndexStore = new IndexStore();
				TransactionStore = new AllTransactionStore();
				var networkWorkFolderPath = Path.Combine(WorkFolderPath, Network.ToString());
				var indexStoreFolderPath = Path.Combine(networkWorkFolderPath, "IndexStore");
				HashChain = new SmartHeaderChain();
				MempoolService = new MempoolService();

				var initTasks = new[]
				{
					IndexStore.InitializeAsync(indexStoreFolderPath, Network, HashChain),
					TransactionStore.InitializeAsync(networkWorkFolderPath, Network)
				};

				await Task.WhenAll(initTasks).ConfigureAwait(false);

				IsInitialized = true;
			}
		}
	}
}
