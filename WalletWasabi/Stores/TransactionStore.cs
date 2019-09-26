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

				var allLines = await TransactionsFileManager.ReadAllLinesAsync();
				var allTransactions = allLines.Select(x => SmartTransaction.FromLine(x, Network));
				lock (TransactionsLock)
				{
					TryAddNoLockNoSerialization(allTransactions);
				}

				if (allTransactions.Count() != Transactions.Count)
				{
					// Another process worked into the file and appended the same transaction into it.
					// In this case we correct the file by serializing the unique set.
					await SerializeAllTransactionsAsync();
				}
			}
			catch (Exception ex)
			{
				// We found a corrupted entry. Stop here.
				// Delete the currupted file.
				// Do not try to autocorrect, because the internal data structures are throwing events that may confuse the consumers of those events.
				Logger.LogError($"{ConfirmedTransactionsFileManager.FileNameWithoutExtension} or {MempoolTransactionsFileManager.FileNameWithoutExtension} file got corrupted. Deleting them...");
				Logger.LogError(ex);
				TransactionsFileManager.DeleteMe();
				throw;
			}
		}
	}
}
