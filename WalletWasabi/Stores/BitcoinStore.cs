using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.Stores.Filters;
using WalletWasabi.Stores.Transactions;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// The purpose of this class is to safely and performantly manage all the Bitcoin related data
	/// that's being serialized to disk, like transactions, wallet files, keys, blocks, index files, etc...
	/// </summary>
	public class BitcoinStore : IStore
	{
		private string WorkFolderPath { get; set; }
		private Network Network { get; set; }

		public IndexStore IndexStore { get; private set; }
		public MempoolStore MempoolStore { get; private set; }
		public ConfirmedTransactionStore ConfirmedTransactionStore { get; private set; }
		public HashChain HashChain => IndexStore.HashChain;

		public async Task InitializeAsync(string workFolderPath, Network network, bool ensureBackwardsCompatibility)
		{
			var initStart = DateTimeOffset.UtcNow;

			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			IoHelpers.EnsureDirectoryExists(WorkFolderPath);

			Network = Guard.NotNull(nameof(network), network);

			IndexStore = new IndexStore();
			MempoolStore = new MempoolStore();
			ConfirmedTransactionStore = new ConfirmedTransactionStore();
			var indexStoreFolderPath = Path.Combine(WorkFolderPath, Network.ToString(), "IndexStore");
			var mempoolStoreFolderPath = Path.Combine(WorkFolderPath, Network.ToString(), "MempoolStore");
			var confirmedTransactionStoreStoreFolderPath = Path.Combine(WorkFolderPath, Network.ToString(), "ConfirmedTransactions");

			var initTasks = new List<Task>
			{
				IndexStore.InitializeAsync(indexStoreFolderPath, Network, ensureBackwardsCompatibility),
				MempoolStore.InitializeAsync(mempoolStoreFolderPath, Network, ensureBackwardsCompatibility),
				ConfirmedTransactionStore.InitializeAsync(confirmedTransactionStoreStoreFolderPath, Network, ensureBackwardsCompatibility)
			};

			await Task.WhenAll(initTasks);

			var elapsedSeconds = Math.Round((DateTimeOffset.UtcNow - initStart).TotalSeconds, 1);
			Logger.LogInfo<BitcoinStore>($"Initialized in {elapsedSeconds} seconds.");
		}
	}
}
