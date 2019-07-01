using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

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

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			IoHelpers.EnsureDirectoryExists(WorkFolderPath);

			Network = Guard.NotNull(nameof(network), network);

			IndexStore = new IndexStore();
			var indexStoreFolderPath = Path.Combine(WorkFolderPath, Network.ToString(), "IndexStore");
			HashChain = new HashChain();
			await IndexStore.InitializeAsync(indexStoreFolderPath, Network, HashChain);
		}
	}
}
