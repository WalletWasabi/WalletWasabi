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
		private MutexIoManager WalletMempoolFileManager { get; set; }
		private HashSet<SmartTransaction> WalletMempool { get; set; }
		private HashSet<uint256> Mempool { get; set; }
		private AsyncLock MempoolLock { get; set; }

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			Network = Guard.NotNull(nameof(network), network);
			var mempoolFilePath = Path.Combine(WorkFolderPath, "WalletMempool.json");
			WalletMempoolFileManager = new MutexIoManager(mempoolFilePath);

			MempoolLock = new AsyncLock();

			using (await MempoolLock.LockAsync())
			using (await WalletMempoolFileManager.Mutex.LockAsync())
			{
				Mempool = new HashSet<uint256>();
				WalletMempool = new HashSet<SmartTransaction>();

				IoHelpers.EnsureDirectoryExists(WorkFolderPath);

				await TryEnsureBackwardsCompatibilityAsync();

				if (Network == Network.RegTest)
				{
					WalletMempoolFileManager.DeleteMe(); // RegTest is not a global ledger, better to delete it.
				}

				await InitializeMempoolAsync();
			}
		}

		private async Task InitializeMempoolAsync()
		{
			try
			{
				if (WalletMempoolFileManager.Exists())
				{
					string jsonString = await File.ReadAllTextAsync(WalletMempoolFileManager.FilePath, Encoding.UTF8);
					IEnumerable<SmartTransaction> transactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString)?
						.OrderByBlockchain();

					foreach (var tx in transactions)
					{
						WalletMempool.Add(tx);
						Mempool.Add(tx.GetHash());
					}
				}
			}
			catch
			{
				// We found a corrupted entry. Stop here.
				// Delete the currupted file.
				// Don't try to autocorrect, because the internal data structures are throwing events those may confuse the consumers of those events.
				Logger.LogError<IndexStore>("Mempool file got corrupted. Deleting...");
				WalletMempoolFileManager.DeleteMe();
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
							var transactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString)?
								.Where(x => !x.Confirmed)? // Only unconfirmed ones.
								.OrderByBlockchain();

							var walletMempoolUpdated = false;
							foreach (var tx in transactions)
							{
								Mempool.Add(tx.GetHash());
								if (WalletMempool.Add(tx))
								{
									walletMempoolUpdated = true;
								}
							}

							if (walletMempoolUpdated)
							{
								string serializedJsonString = JsonConvert.SerializeObject(WalletMempool.OrderByBlockchain(), Formatting.Indented);
								await File.WriteAllTextAsync(WalletMempoolFileManager.FilePath, serializedJsonString, Encoding.UTF8);
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

		public async void ProcessAsync(params uint256[] txids)
		{
			using (await MempoolLock.LockAsync())
			{
				foreach (var txid in txids)
				{
					Mempool.Add(txid);
				}
			}
		}

		public async void ProcessAsync(bool isWalletRelevant, params SmartTransaction[] txs)
		{
			using (await MempoolLock.LockAsync())
			{
				var walletMempoolUpdated = false;

				foreach (var tx in txs)
				{
					Mempool.Add(tx.GetHash());
					if (isWalletRelevant)
					{
						if (WalletMempool.Add(tx))
						{
							walletMempoolUpdated = true;
						}
					}
				}

				if (walletMempoolUpdated)
				{
					using (await WalletMempoolFileManager.Mutex.LockAsync())
					{
						try
						{
							// Serialize, but first read it out.
							string jsonString = await File.ReadAllTextAsync(WalletMempoolFileManager.FilePath, Encoding.UTF8);
							var transactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString)?
								.Where(x => !x.Confirmed)? // Only unconfirmed ones.
								.OrderByBlockchain();

							// If the two are already the same then some other software instance already added it.
							if (WalletMempool.Intersect(transactions).Count() == WalletMempool.Count)
							{
								return;
							}

							// If another software instance added more to the serialized file, then add them to our mempool, too.
							foreach (var tx in transactions)
							{
								Mempool.Add(tx.GetHash());
								WalletMempool.Add(tx);
							}
						}
						catch (Exception ex)
						{
							Logger.LogError<MempoolStore>(ex);
						}

						// Finally serialize.
						string serializedJsonString = JsonConvert.SerializeObject(WalletMempool.OrderByBlockchain(), Formatting.Indented);
						await File.WriteAllTextAsync(WalletMempoolFileManager.FilePath, serializedJsonString, Encoding.UTF8);
					}
				}
			}
		}

		#endregion MempoolOperations
	}
}
