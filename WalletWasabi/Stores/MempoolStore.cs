using NBitcoin;
using Newtonsoft.Json;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// Manages to store the mempool safely.
	/// </summary>
	public class MempoolStore
	{
		private string WorkFolderPath { get; set; }
		private Network Network { get; set; }
		private MutexIoManager MempoolTransactionsFileManager { get; set; }

		public MempoolCache MempoolCache { get; private set; }
		private AsyncLock MempoolLock { get; set; }

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			Network = Guard.NotNull(nameof(network), network);
			var mempoolFilePath = Path.Combine(WorkFolderPath, "MempoolTransactions.json");
			MempoolTransactionsFileManager = new MutexIoManager(mempoolFilePath);
			MempoolCache = new MempoolCache();

			MempoolLock = new AsyncLock();

			using (await MempoolLock.LockAsync())
			using (await MempoolTransactionsFileManager.Mutex.LockAsync())
			{
				IoHelpers.EnsureDirectoryExists(WorkFolderPath);

				await TryEnsureBackwardsCompatibilityAsync();

				if (Network == Network.RegTest)
				{
					MempoolTransactionsFileManager.DeleteMe(); // RegTest is not a global ledger, better to delete it.
				}

				await InitializeMempoolAsync();
			}
		}

		private async Task InitializeMempoolAsync()
		{
			try
			{
				if (MempoolTransactionsFileManager.Exists())
				{
					string jsonString = await File.ReadAllTextAsync(MempoolTransactionsFileManager.FilePath, Encoding.UTF8);
					var transactions = MempoolCache.MempoolTransactionsFromJson(jsonString).ToArray();
					MempoolCache.TryAddHashesAndTransactions(transactions);
				}
			}
			catch
			{
				// We found a corrupted entry. Stop here.
				// Delete the currupted file.
				// Don't try to autocorrect, because the internal data structures are throwing events those may confuse the consumers of those events.
				Logger.LogError<IndexStore>("Mempool file got corrupted. Deleting...");
				MempoolTransactionsFileManager.DeleteMe();
				throw;
			}
		}

		private async Task TryEnsureBackwardsCompatibilityAsync()
		{
			try
			{
				// Before Wasabi 1.1.6
				var parentFolder = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), "Transactions");
				var oldTransactionsFolderPath = Path.Combine(parentFolder, Network.ToString());

				if (Directory.Exists(oldTransactionsFolderPath))
				{
					foreach (var txFile in Directory.EnumerateFiles(oldTransactionsFolderPath))
					{
						try
						{
							string jsonString = await File.ReadAllTextAsync(txFile, Encoding.UTF8);
							var transactions = MempoolCache.MempoolTransactionsFromJson(jsonString)?.ToArray();

							if (MempoolCache.TryAddHashesAndTransactions(transactions))
							{
								string serializedJsonString = MempoolCache.MempoolTransactionsToJson();
								await File.WriteAllTextAsync(MempoolTransactionsFileManager.FilePath, serializedJsonString, Encoding.UTF8);
							}

							// Uncomment this deletion if this code would be merged to the master. The developer forgot it.
							// File.Delete(txFile);
						}
						catch (Exception ex)
						{
							Logger.LogWarning<MempoolStore>(ex);
						}
					}

					// Uncomment this deletion if this code would be merged to the master. The developer forgot it.
					//Directory.Delete(oldTransactionsFolderPath);
				}

				if (Directory.Exists(parentFolder) && Directory.EnumerateDirectories(parentFolder).Count() == 0) // Only delete it if it's empty, because other networks' compatibility is not ensured.
				{
					// Uncomment this deletion if this code would be merged to the master. The developer forgot it.
					//Directory.Delete(parentFolder);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<IndexStore>($"Backwards compatibility couldn't be ensured. Exception: {ex.ToString()}");
			}
		}

		#region MempoolOperations

		public async Task<(int removedHashCount, int removedTxCount)> CleanupAsync(ISet<string> allMempoolHashes, int compactness)
		{
			if (allMempoolHashes is null || !allMempoolHashes.Any())
			{
				return (0, 0);
			}

			using (await MempoolLock.LockAsync())
			{
				var removedCounts = MempoolCache.Cleanup(allMempoolHashes, compactness);

				if (removedCounts.removedTxCount > 0)
				{
					await SerializeTransactionsAsync();
				}

				return removedCounts;
			}
		}

		public async Task ProcessAsync(params uint256[] txids)
		{
			using (await MempoolLock.LockAsync())
			{
				MempoolCache.TryAddHashes(txids);
			}
		}

		public async Task ProcessAsync(params SmartTransaction[] txs)
		{
			using (await MempoolLock.LockAsync())
			{
				if (MempoolCache.TryAddHashesAndTransactions(txs))
				{
					await SerializeTransactionsAsync();
				}
			}
		}

		private async Task SerializeTransactionsAsync()
		{
			using (await MempoolTransactionsFileManager.Mutex.LockAsync())
			{
				try
				{
					// Serialize, but first read it out.
					string jsonString = await File.ReadAllTextAsync(MempoolTransactionsFileManager.FilePath, Encoding.UTF8);
					var transactions = MempoolCache.MempoolTransactionsFromJson(jsonString)?.ToArray();

					// If the two are already the same then some other software instance already added it.
					if (MempoolCache.SetEquals(transactions))
					{
						return;
					}

					// If another software instance added more to the serialized file, then add them to our mempool, too.
					MempoolCache.TryAddHashesAndTransactions(transactions);
				}
				catch (Exception ex)
				{
					Logger.LogError<MempoolStore>(ex);
				}

				// Finally serialize.
				string serializedJsonString = MempoolCache.MempoolTransactionsToJson();
				await File.WriteAllTextAsync(MempoolTransactionsFileManager.FilePath, serializedJsonString, Encoding.UTF8);
			}
		}

		#endregion MempoolOperations
	}
}
