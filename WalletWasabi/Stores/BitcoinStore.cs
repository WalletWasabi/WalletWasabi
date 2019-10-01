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
		public HashChain HashChain { get; private set; }

		public MempoolService MempoolService { get; private set; }

		/// <summary>
		/// This should not be a property, but a creator function, because it'll be cloned left and right by NBitcoin later.
		/// So it should not be assumed it's some singleton.
		/// </summary>
		public MempoolBehavior CreateMempoolBehavior() => new MempoolBehavior(MempoolService);

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			using (BenchmarkLogger.Measure())
			{
				WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
				IoHelpers.EnsureDirectoryExists(WorkFolderPath);

				Network = Guard.NotNull(nameof(network), network);

				MempoolService = new MempoolService();

				IndexStore = new IndexStore();
				var indexStoreFolderPath = Path.Combine(WorkFolderPath, Network.ToString(), "IndexStore");
				HashChain = new HashChain();
				await IndexStore.InitializeAsync(indexStoreFolderPath, Network, HashChain);
			}
		}
	}
}
