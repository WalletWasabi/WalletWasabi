using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Mempool;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// The purpose of this class is to safely and performantly manage all the Bitcoin related data
	/// that's being serialized to disk, like transactions, wallet files, keys, blocks, index files, etc...
	/// </summary>
	public class BitcoinStore
	{
		private string WorkFolderPath { get; set; }
		private Network Network { get; set; }

		public IndexStore IndexStore { get; private set; }
		public TransactionStore TransactionStore { get; private set; }

		public HashChain HashChain => IndexStore.HashChain;
		public MempoolService MempoolService => TransactionStore.MempoolService;

		public MempoolBehavior CreateMempoolBehavior() => MempoolService.CreateMempoolBehavior();

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			using (BenchmarkLogger.Measure())
			{
				WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
				IoHelpers.EnsureDirectoryExists(WorkFolderPath);

				Network = Guard.NotNull(nameof(network), network);

				IndexStore = new IndexStore();
				TransactionStore = new TransactionStore();

				var indexStoreFolderPath = Path.Combine(WorkFolderPath, Network.ToString(), "IndexStore");
				var transactionStoreFolderPath = Path.Combine(WorkFolderPath, Network.ToString(), "TransactionStore");

				var initTasks = new[]
				{
					IndexStore.InitializeAsync(indexStoreFolderPath, Network),
					TransactionStore.InitializeAsync(transactionStoreFolderPath, Network)
				};

				await Task.WhenAll(initTasks);
			}
		}
	}
}
