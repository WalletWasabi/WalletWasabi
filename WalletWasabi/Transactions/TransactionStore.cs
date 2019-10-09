using NBitcoin;
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

namespace WalletWasabi.Transactions
{
	public class TransactionStore
	{
		#region Initializers

		public string WorkFolderPath { get; private set; }
		public Network Network { get; private set; }

		private Dictionary<uint256, SmartTransaction> Transactions { get; set; }
		private object TransactionsLock { get; set; }
		private MutexIoManager TransactionsFileManager { get; set; }

		public async Task InitializeAsync(string workFolderPath, Network network, string operationName)
		{
			using (BenchmarkLogger.Measure(operationName: operationName))
			{
				WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
				Network = Guard.NotNull(nameof(network), network);

				Transactions = new Dictionary<uint256, SmartTransaction>();
				TransactionsLock = new object();

				var fileName = Path.Combine(WorkFolderPath, "Transactions.dat");
				var transactionsFilePath = Path.Combine(WorkFolderPath, fileName);
				TransactionsFileManager = new MutexIoManager(transactionsFilePath);

				using (await TransactionsFileManager.Mutex.LockAsync().ConfigureAwait(false))
				{
					IoHelpers.EnsureDirectoryExists(WorkFolderPath);

					if (!TransactionsFileManager.Exists())
					{
						await SerializeAllTransactionsNoMutexAsync().ConfigureAwait(false);
					}

					await InitializeTransactionsNoMutexAsync().ConfigureAwait(false);
				}
			}
		}

		private async Task InitializeTransactionsNoMutexAsync()
		{
			try
			{
				IoHelpers.EnsureFileExists(TransactionsFileManager.FilePath);

				var allLines = await TransactionsFileManager.ReadAllLinesAsync().ConfigureAwait(false);
				var allTransactions = allLines
					.Select(x => SmartTransaction.FromLine(x, Network))
					.OrderByBlockchain();

				lock (TransactionsLock)
				{
					foreach (var tx in allTransactions)
					{
						TryAddNoLockNoSerialization(tx);
					}
				}

				if (allTransactions.Count() != Transactions.Count)
				{
					// Another process worked into the file and appended the same transaction into it.
					// In this case we correct the file by serializing the unique set.
					await SerializeAllTransactionsNoMutexAsync().ConfigureAwait(false);
				}
			}
			catch
			{
				// We found a corrupted entry. Stop here.
				// Delete the currupted file.
				// Do not try to autocorrect, because the internal data structures are throwing events that may confuse the consumers of those events.
				Logger.LogError($"{TransactionsFileManager.FileNameWithoutExtension} file got corrupted. Deleting it...");
				TransactionsFileManager.DeleteMe();
				throw;
			}
		}

		#endregion Initializers

		#region Modifiers

		public bool TryAdd(SmartTransaction tx)
		{
			bool isAdded;

			lock (TransactionsLock)
			{
				isAdded = TryAddNoLockNoSerialization(tx);
			}

			if (isAdded)
			{
				_ = TryAppendToFileAsync(tx);
			}

			return isAdded;
		}

		private bool TryAddNoLockNoSerialization(SmartTransaction tx)
		{
			var hash = tx.GetHash();

			if (Transactions.TryAdd(hash, tx))
			{
				return true;
			}
			else
			{
				Transactions[hash].TryUpdate(tx);
				return false;
			}
		}

		public bool TryRemove(uint256 hash, out SmartTransaction stx)
		{
			bool isRemoved;

			lock (TransactionsLock)
			{
				isRemoved = Transactions.Remove(hash, out stx);
			}

			if (isRemoved)
			{
				_ = TryRemoveFromFileAsync(hash);
			}

			return isRemoved;
		}

		#endregion Modifiers

		#region Accessors

		public bool TryGetTransaction(uint256 hash, out SmartTransaction sameStx)
		{
			lock (TransactionsLock)
			{
				return Transactions.TryGetValue(hash, out sameStx);
			}
		}

		public IEnumerable<SmartTransaction> GetTransactions()
		{
			lock (TransactionsLock)
			{
				return Transactions.Values.OrderByBlockchain().ToList();
			}
		}

		public IEnumerable<uint256> GetTransactionHashes()
		{
			lock (TransactionsLock)
			{
				return Transactions.Values.OrderByBlockchain().Select(x => x.GetHash()).ToList();
			}
		}

		public bool IsEmpty()
		{
			lock (TransactionsLock)
			{
				return !Transactions.Any();
			}
		}

		public bool Contains(uint256 hash)
		{
			lock (TransactionsLock)
			{
				return Transactions.ContainsKey(hash);
			}
		}

		#endregion Accessors

		#region Serialization

		private async Task SerializeAllTransactionsNoMutexAsync()
		{
			List<SmartTransaction> transactionsClone;
			lock (TransactionsLock)
			{
				transactionsClone = Transactions.Values.ToList();
			}

			await TransactionsFileManager.WriteAllLinesAsync(transactionsClone.ToBlockchainOrderedLines()).ConfigureAwait(false);
		}

		private async Task TryAppendToFileAsync(params SmartTransaction[] transactions)
			=> await TryAppendToFileAsync(transactions as IEnumerable<SmartTransaction>).ConfigureAwait(false);

		private async Task TryAppendToFileAsync(IEnumerable<SmartTransaction> transactions)
			=> await TryCommitToFileAsync(new Append(transactions)).ConfigureAwait(false);

		private async Task TryRemoveFromFileAsync(params uint256[] transactionIds)
			=> await TryRemoveFromFileAsync(transactionIds as IEnumerable<uint256>).ConfigureAwait(false);

		private async Task TryRemoveFromFileAsync(IEnumerable<uint256> transactionIds)
			=> await TryCommitToFileAsync(new Remove(transactionIds)).ConfigureAwait(false);

		private List<ITxStoreOperation> Operations { get; } = new List<ITxStoreOperation>();
		private object OperationsLock { get; } = new object();

		private async Task TryCommitToFileAsync(ITxStoreOperation operation)
		{
			try
			{
				if (operation is null || operation.IsEmpty)
				{
					return;
				}

				// Make sure that only one call can continue.
				lock (OperationsLock)
				{
					var isRunning = Operations.Any();
					Operations.Add(operation);
					if (isRunning)
					{
						return;
					}
				}

				// Wait until the operation list calms down.
				IEnumerable<ITxStoreOperation> operationsToExecute;
				while (true)
				{
					var count = Operations.Count;

					await Task.Delay(100).ConfigureAwait(false);

					lock (OperationsLock)
					{
						if (count == Operations.Count)
						{
							// Merge operations.
							operationsToExecute = OperationMerger.Merge(Operations).ToList();
							Operations.Clear();
							break;
						}
					}
				}

				using (await TransactionsFileManager.Mutex.LockAsync().ConfigureAwait(false))
				{
					foreach (ITxStoreOperation op in operationsToExecute)
					{
						if (op is Append appendOperation)
						{
							var toAppends = appendOperation.Transactions;

							try
							{
								await TransactionsFileManager.AppendAllLinesAsync(toAppends.ToBlockchainOrderedLines()).ConfigureAwait(false);
							}
							catch
							{
								await SerializeAllTransactionsNoMutexAsync().ConfigureAwait(false);
							}
						}
						else if (op is Remove removeOperation)
						{
							var toRemoves = removeOperation.Transactions;

							string[] allLines = await TransactionsFileManager.ReadAllLinesAsync().ConfigureAwait(false);
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
								await TransactionsFileManager.WriteAllLinesAsync(toSerialize).ConfigureAwait(false);
							}
							catch
							{
								await SerializeAllTransactionsNoMutexAsync().ConfigureAwait(false);
							}
						}
						else
						{
							throw new NotSupportedException();
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		#endregion Serialization
	}
}
