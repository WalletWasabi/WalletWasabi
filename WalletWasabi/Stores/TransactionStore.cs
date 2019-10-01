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

namespace WalletWasabi.Stores
{
	public class TransactionStore
	{
		public string WorkFolderPath { get; private set; }
		public Network Network { get; private set; }

		private Dictionary<uint256, SmartTransaction> Transactions { get; set; }
		private object TransactionsLock { get; set; }
		private MutexIoManager TransactionsFileManager { get; set; }

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			using (BenchmarkLogger.Measure())
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

		private async Task SerializeAllTransactionsNoMutexAsync()
		{
			List<SmartTransaction> transactionsClone;
			lock (TransactionsLock)
			{
				transactionsClone = Transactions.Values.ToList();
			}

			await TransactionsFileManager.WriteAllLinesAsync(transactionsClone.OrderByBlockchain().Select(x => x.ToLine())).ConfigureAwait(false);
		}

		private async Task InitializeTransactionsNoMutexAsync()
		{
			try
			{
				IoHelpers.EnsureFileExists(TransactionsFileManager.FilePath);

				var allLines = await TransactionsFileManager.ReadAllLinesAsync().ConfigureAwait(false);
				var allTransactions = allLines.Select(x => SmartTransaction.FromLine(x, Network));

				lock (TransactionsLock)
				{
					TryAddNoLockNoSerialization(allTransactions);
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

		private ISet<SmartTransaction> TryAddNoLockNoSerialization(IEnumerable<SmartTransaction> transactions)
		{
			transactions ??= Enumerable.Empty<SmartTransaction>();
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

		public bool TryAdd(SmartTransaction tx)
		{
			lock (TransactionsLock)
			{
				var isAdded = TryAddNoLockNoSerialization(tx);
				if (isAdded)
				{
					_ = TryAppendToFileAsync(tx);
				}

				return isAdded;
			}
		}

		public ISet<SmartTransaction> TryAdd(IEnumerable<SmartTransaction> transactions)
		{
			ISet<SmartTransaction> added;
			lock (TransactionsLock)
			{
				added = TryAddNoLockNoSerialization(transactions);
				if (added.Any())
				{
					_ = TryAppendToFileAsync(transactions);
				}
			}
			return added;
		}

		private bool TryAddNoLockNoSerialization(SmartTransaction tx)
		{
			var hash = tx.GetHash();
			return Transactions.TryAdd(hash, tx);
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
			hashes ??= Enumerable.Empty<uint256>();
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
			}

			return removed;
		}

		private async Task TryAppendToFileAsync(SmartTransaction stx)
		{
			try
			{
				using (await TransactionsFileManager.Mutex.LockAsync().ConfigureAwait(false))
				{
					try
					{
						await TransactionsFileManager.AppendAllLinesAsync(new[] { stx.ToLine() }).ConfigureAwait(false);
					}
					catch
					{
						await SerializeAllTransactionsNoMutexAsync().ConfigureAwait(false);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		private async Task TryAppendToFileAsync(IEnumerable<SmartTransaction> transactions)
		{
			try
			{
				using (await TransactionsFileManager.Mutex.LockAsync().ConfigureAwait(false))
				{
					try
					{
						await TransactionsFileManager.AppendAllLinesAsync(transactions.Select(x => x.ToLine())).ConfigureAwait(false);
					}
					catch
					{
						await SerializeAllTransactionsNoMutexAsync().ConfigureAwait(false);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
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

				using (await TransactionsFileManager.Mutex.LockAsync().ConfigureAwait(false))
				{
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
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		public bool TryGetTransaction(uint256 hash, out SmartTransaction sameStx)
		{
			lock (TransactionsLock)
			{
				return Transactions.TryGetValue(hash, out sameStx);
			}
		}

		public ISet<SmartTransaction> GetTransactions()
		{
			lock (TransactionsLock)
			{
				return Transactions.Values.ToHashSet();
			}
		}

		public ISet<uint256> GetTransactionHashes()
		{
			lock (TransactionsLock)
			{
				return Transactions.Keys.ToHashSet();
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
	}
}
