using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Mempool;
using WalletWasabi.Models;

namespace WalletWasabi.Stores
{
	public class TransactionStore
	{
		public string WorkFolderPath { get; private set; }
		public Network Network { get; private set; }

		private Dictionary<uint256, SmartTransaction> MempoolTransactions { get; set; }
		private Dictionary<uint256, SmartTransaction> ConfirmedTransactions { get; set; }

		private object TransactionsLock { get; set; }
		private MutexIoManager ConfirmedTransactionsFileManager { get; set; }
		private MutexIoManager MempoolTransactionsFileManager { get; set; }

		public MempoolService MempoolService { get; private set; }

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			using (BenchmarkLogger.Measure())
			{
				WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
				Network = Guard.NotNull(nameof(network), network);

				MempoolTransactions = new Dictionary<uint256, SmartTransaction>();
				ConfirmedTransactions = new Dictionary<uint256, SmartTransaction>();
				TransactionsLock = new object();

				MempoolService = new MempoolService();

				var confirmedTransactionsFilePath = Path.Combine(WorkFolderPath, "ConfirmedTransactions.dat");
				ConfirmedTransactionsFileManager = new MutexIoManager(confirmedTransactionsFilePath);

				var mempoolTransactionsFilePath = Path.Combine(WorkFolderPath, "MempoolTransactions.dat");
				MempoolTransactionsFileManager = new MutexIoManager(mempoolTransactionsFilePath);

				using (await ConfirmedTransactionsFileManager.Mutex.LockAsync())
				using (await MempoolTransactionsFileManager.Mutex.LockAsync())
				{
					IoHelpers.EnsureDirectoryExists(WorkFolderPath);

					TryEnsureBackwardsCompatibility();

					if (Network == Network.RegTest)
					{
						// RegTest is not a global ledger, better to delete it.
						ConfirmedTransactionsFileManager.DeleteMe();
						MempoolTransactionsFileManager.DeleteMe();
					}

					// If none of them exists, then serialize all the transactions we loaded in from backwards compatibility ensurement.
					if (!ConfirmedTransactionsFileManager.Exists() && !MempoolTransactionsFileManager.Exists())
					{
						await SerializeAllTransactionsNoMutexAsync();
					}

					await InitializeTransactionsNoMutexAsync();
				}
			}
		}

		private void TryEnsureBackwardsCompatibility()
		{
			try
			{
				// Before Wasabi 1.1.7
				var oldTransactionsFolderPath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), "Transactions", Network.Name);
				if (Directory.Exists(oldTransactionsFolderPath))
				{
					foreach (var filePath in Directory.EnumerateFiles(oldTransactionsFolderPath))
					{
						try
						{
							string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
							var allTransactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString)?.OrderByBlockchain() ?? Enumerable.Empty<SmartTransaction>();

							lock (TransactionsLock)
							{
								TryAddNoLockNoSerialization(allTransactions);
							}

							// ToDo: Uncomment when PR is finished.
							// File.Delete(filePath);
						}
						catch (Exception ex)
						{
							Logger.LogTrace(ex);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"Backwards compatibility could not be ensured.");
				Logger.LogWarning(ex);
			}
		}

		private async Task SerializeAllTransactionsNoMutexAsync()
		{
			List<SmartTransaction> confirmedTransactionsClone;
			List<SmartTransaction> mempoolTransactionsClone;
			lock (TransactionsLock)
			{
				confirmedTransactionsClone = ConfirmedTransactions.Values.ToList();
				mempoolTransactionsClone = MempoolTransactions.Values.ToList();
			}

			await ConfirmedTransactionsFileManager.WriteAllLinesAsync(confirmedTransactionsClone.OrderByBlockchain().Select(x => x.ToLine()));
			await MempoolTransactionsFileManager.WriteAllLinesAsync(mempoolTransactionsClone.OrderByBlockchain().Select(x => x.ToLine()));
		}

		private async Task InitializeTransactionsNoMutexAsync()
		{
			try
			{
				IoHelpers.EnsureFileExists(ConfirmedTransactionsFileManager.FilePath);
				IoHelpers.EnsureFileExists(MempoolTransactionsFileManager.FilePath);

				var readTasks = new[]
				{
					ConfirmedTransactionsFileManager.ReadAllLinesAsync(),
					MempoolTransactionsFileManager.ReadAllLinesAsync()
				};

				var allTransactions = (await readTasks[1]).Concat(await readTasks[0]).Select(x => SmartTransaction.FromLine(x, Network));
				lock (TransactionsLock)
				{
					TryAddNoLockNoSerialization(allTransactions);
				}

				if (allTransactions.Count() != (MempoolTransactions.Count + ConfirmedTransactions.Count))
				{
					// Another process worked into the file and appended the same transaction into it.
					// In this case we correct the file by serializing the unique set.
					await SerializeAllTransactionsNoMutexAsync();
				}
			}
			catch
			{
				// We found a corrupted entry. Stop here.
				// Delete the currupted file.
				// Do not try to autocorrect, because the internal data structures are throwing events that may confuse the consumers of those events.
				Logger.LogError($"{ConfirmedTransactionsFileManager.FileNameWithoutExtension} or {MempoolTransactionsFileManager.FileNameWithoutExtension} file got corrupted. Deleting them...");
				ConfirmedTransactionsFileManager.DeleteMe();
				MempoolTransactionsFileManager.DeleteMe();

				throw;
			}
		}

		protected ISet<SmartTransaction> TryAddNoLockNoSerialization(IEnumerable<SmartTransaction> transactions)
		{
			transactions = transactions ?? Enumerable.Empty<SmartTransaction>();
			var added = new HashSet<SmartTransaction>();
			foreach (var tx in transactions)
			{
				if (TryAddNoLockNoSerialization(tx))
				{
					added.Add(tx);
				}
			}

			return added;
		}

		private bool TryAddNoLockNoSerialization(SmartTransaction tx)
		{
			var hash = tx.GetHash();

			bool added = false;
			if (tx.Confirmed)
			{
				MempoolTransactions.Remove(hash);
				added = ConfirmedTransactions.TryAdd(hash, tx);
			}
			else
			{
				if (!ConfirmedTransactions.ContainsKey(hash))
				{
					added = MempoolTransactions.TryAdd(hash, tx);
				}
			}
			return added;
		}

		public bool TryRemove(uint256 hash, out SmartTransaction stx)
		{
			lock (TransactionsLock)
			{
				var isRemoved = Transactions.Remove(hash, out stx);

				if (isRemoved)
				{
					_ = TryRemoveFromFileAsync(hash);
				}

				return isRemoved;
			}
		}

		public ISet<SmartTransaction> TryRemove(IEnumerable<uint256> hashes)
		{
			hashes = hashes ?? Enumerable.Empty<uint256>();
			var removed = new HashSet<SmartTransaction>();

			lock (TransactionsLock)
			{
				foreach (var hash in hashes)
				{
					if (Transactions.Remove(hash, out SmartTransaction stx))
					{
						removed.Add(stx);
					}
				}

				if (removed.Any())
				{
					_ = TryRemoveFromFileAsync(removed.Select(x => x.GetHash()).ToArray());
				}

				return removed;
			}
		}

		private async Task TryRemoveFromFileAsync(params uint256[] toRemoves)
		{
			try
			{
				if (toRemoves is null || !toRemoves.Any())
				{
					return;
				}

				using (await TransactionsFileManager.Mutex.LockAsync())
				{
					string[] allLines = await TransactionsFileManager.ReadAllLinesAsync();
					var toSerialize = new List<string>();
					foreach (var line in allLines)
					{
						var startsWith = false;
						foreach (var toRemoveString in toRemoves.Select(x => x.ToString()))
						{
							startsWith = startsWith || line.StartsWith(toRemoveString, StringComparison.Ordinal);
						}

						if (!startsWith)
						{
							toSerialize.Add(line);
						}
					}

					try
					{
						await TransactionsFileManager.WriteAllLinesAsync(toSerialize);
					}
					catch
					{
						await SerializeAllTransactionsAsync();
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError<MempoolStore>(ex);
			}
		}

		public SmartTransaction[] GetConfirmedTransactions()
		{
			lock (TransactionsLock)
			{
				return ConfirmedTransactions.Values.OrderByBlockchain().ToArray();
			}
		}

		public SmartTransaction[] GetMempoolTransactions()
		{
			lock (TransactionsLock)
			{
				return MempoolTransactions.Values.OrderByBlockchain().ToArray();
			}
		}
	}
}
