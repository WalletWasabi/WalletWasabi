using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions.Operations;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Nito.AsyncEx;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionStore : IAsyncDisposable
{
	public TransactionStore(string workFolderPath, Network network)
	{
		WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
		Network = network;

		// In Transactions.dat every line starts with the tx id, so the first character is the best for digest creation.
		TransactionsFileManager = new IoManager(filePath: Path.Combine(WorkFolderPath, "Transactions.dat"));
	}

	public string WorkFolderPath { get; }
	public Network Network { get; }

	private Dictionary<uint256, SmartTransaction> Transactions { get; } = new();
	private object TransactionsLock { get; } = new();
	private IoManager TransactionsFileManager { get; set; }
	private AsyncLock TransactionsFileAsyncLock { get; } = new();
	private List<ITxStoreOperation> Operations { get; } = new();
	private object OperationsLock { get; } = new();

	private AbandonedTasks AbandonedTasks { get; } = new();

	public async Task InitializeAsync(string operationName, CancellationToken cancel)
	{
		using (BenchmarkLogger.Measure(operationName: operationName))
		{
			cancel.ThrowIfCancellationRequested();
			using (await TransactionsFileAsyncLock.LockAsync(cancel).ConfigureAwait(false))
			{
				IoHelpers.EnsureDirectoryExists(WorkFolderPath);
				cancel.ThrowIfCancellationRequested();

				if (!TransactionsFileManager.Exists())
				{
					await SerializeAllTransactionsNoLockAsync().ConfigureAwait(false);
					cancel.ThrowIfCancellationRequested();
				}

				await InitializeTransactionsNoLockAsync(cancel).ConfigureAwait(false);
			}
		}
	}

	private async Task InitializeTransactionsNoLockAsync(CancellationToken cancel)
	{
		try
		{
			IoHelpers.EnsureFileExists(TransactionsFileManager.FilePath);
			cancel.ThrowIfCancellationRequested();

			var allLines = await TransactionsFileManager.ReadAllLinesAsync(cancel).ConfigureAwait(false);
			var allTransactions = allLines
				.Select(x => SmartTransaction.FromLine(x, Network))
				.OrderByBlockchain();

			var added = false;
			var updated = false;
			lock (TransactionsLock)
			{
				foreach (var tx in allTransactions)
				{
					var (isAdded, isUpdated) = TryAddOrUpdateNoLockNoSerialization(tx);
					if (isAdded)
					{
						added = true;
					}
					if (isUpdated)
					{
						updated = true;
					}
				}
			}

			if (added || updated)
			{
				cancel.ThrowIfCancellationRequested();

				// Another process worked into the file and appended the same transaction into it.
				// In this case we correct the file by serializing the unique set.
				await SerializeAllTransactionsNoLockAsync().ConfigureAwait(false);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// We found a corrupted entry. Stop here.
			// Delete the corrupted file.
			// Do not try to automatically correct the data, because the internal data structures are throwing events that may confuse the consumers of those events.
			Logger.LogError($"{TransactionsFileManager.FileNameWithoutExtension} file got corrupted. Deleting it...");
			TransactionsFileManager.DeleteMe();
			throw;
		}
	}

	#region Modifiers

	public (bool isAdded, bool isUpdated) TryAddOrUpdate(SmartTransaction tx)
	{
		(bool isAdded, bool isUpdated) ret;

		lock (TransactionsLock)
		{
			ret = TryAddOrUpdateNoLockNoSerialization(tx);
		}

		if (ret.isAdded)
		{
			AbandonedTasks.AddAndClearCompleted(TryAppendToFileAsync(tx));
		}

		if (ret.isUpdated)
		{
			AbandonedTasks.AddAndClearCompleted(TryUpdateFileAsync(tx));
		}

		return ret;
	}

	private (bool isAdded, bool isUpdated) TryAddOrUpdateNoLockNoSerialization(SmartTransaction tx)
	{
		var hash = tx.GetHash();

		if (Transactions.TryAdd(hash, tx))
		{
			return (true, false);
		}
		else
		{
			if (Transactions[hash].TryUpdate(tx))
			{
				return (false, true);
			}
			else
			{
				return (false, false);
			}
		}
	}

	public bool TryUpdate(SmartTransaction tx)
	{
		bool ret;
		lock (TransactionsLock)
		{
			ret = TryUpdateNoLockNoSerialization(tx);
		}

		if (ret)
		{
			AbandonedTasks.AddAndClearCompleted(TryUpdateFileAsync(tx));
		}

		return ret;
	}

	private bool TryUpdateNoLockNoSerialization(SmartTransaction tx)
	{
		var hash = tx.GetHash();

		if (Transactions.TryGetValue(hash, out var found))
		{
			return found.TryUpdate(tx);
		}

		return false;
	}

	public bool TryRemove(uint256 hash, [NotNullWhen(true)] out SmartTransaction? stx)
	{
		bool isRemoved;

		lock (TransactionsLock)
		{
			isRemoved = Transactions.Remove(hash, out stx);
		}

		if (isRemoved)
		{
			AbandonedTasks.AddAndClearCompleted(TryRemoveFromFileAsync(hash));
		}

		return isRemoved;
	}

	#endregion Modifiers

	#region Accessors

	public bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? sameStx)
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
			return Transactions.Count == 0;
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

	private async Task SerializeAllTransactionsNoLockAsync()
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

	private async Task TryUpdateFileAsync(params SmartTransaction[] transactions)
		=> await TryUpdateFileAsync(transactions as IEnumerable<SmartTransaction>).ConfigureAwait(false);

	private async Task TryUpdateFileAsync(IEnumerable<SmartTransaction> transactions)
		=> await TryCommitToFileAsync(new Update(transactions)).ConfigureAwait(false);

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
				var isRunning = Operations.Count != 0;
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

			using (await TransactionsFileAsyncLock.LockAsync().ConfigureAwait(false))
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
							await SerializeAllTransactionsNoLockAsync().ConfigureAwait(false);
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
							await SerializeAllTransactionsNoLockAsync().ConfigureAwait(false);
						}
					}
					else if (op is Update updateOperation)
					{
						var toUpdates = updateOperation.Transactions;

						string[] allLines = await TransactionsFileManager.ReadAllLinesAsync().ConfigureAwait(false);
						IEnumerable<SmartTransaction> allTransactions = allLines.Select(x => SmartTransaction.FromLine(x, Network));
						var toSerialize = new List<SmartTransaction>();

						foreach (SmartTransaction tx in allTransactions)
						{
							var txsToUpdateWith = toUpdates.Where(x => x == tx);
							foreach (var txToUpdateWith in txsToUpdateWith)
							{
								tx.TryUpdate(txToUpdateWith);
							}
							toSerialize.Add(tx);
						}

						try
						{
							await TransactionsFileManager.WriteAllLinesAsync(toSerialize.ToBlockchainOrderedLines()).ConfigureAwait(false);
						}
						catch
						{
							await SerializeAllTransactionsNoLockAsync().ConfigureAwait(false);
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

	public async ValueTask DisposeAsync()
	{
		await AbandonedTasks.WhenAllAsync().ConfigureAwait(false);
	}
}
