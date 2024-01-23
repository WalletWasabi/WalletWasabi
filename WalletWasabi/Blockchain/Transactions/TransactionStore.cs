using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Stores;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionStore : IAsyncDisposable
{
	public TransactionStore(string workFolderPath, Network network)
	{
		DataSource = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);

		bool useInMemoryDatabase = DataSource == SqliteStorageHelper.InMemoryDatabase;

		if (!useInMemoryDatabase)
		{
			IoHelpers.EnsureDirectoryExists(DataSource);
			DataSource = Path.Combine(DataSource, "Transactions.sqlite");
		}

		SqliteStorage = TransactionSqliteStorage.FromFile(dataSource: DataSource, network);

		// Migrate data.
		if (!useInMemoryDatabase)
		{
			string oldPath = Path.Combine(Path.GetDirectoryName(DataSource)!, "Transactions.dat");
			Import(oldPath, DataSource, network);
		}
	}

	private string DataSource { get; }
	private object SqliteStorageLock { get; } = new();

	/// <remarks>Guarded by <see cref="SqliteStorageLock"/>.</remarks>
	private TransactionSqliteStorage SqliteStorage { get; }

	/// <remarks>Guarded by <see cref="SqliteStorageLock"/>.</remarks>
	private Dictionary<uint256, SmartTransaction> Transactions { get; } = new();

	// ToDo: Temporary to fix https://github.com/zkSNACKs/WalletWasabi/pull/12137#issuecomment-1879798750
	public bool NeedResync { get; private set; }

	private void Import(string oldPath, string dbPath, Network network)
	{
		if (File.Exists(oldPath))
		{
			Logger.LogInfo($"Migration of transaction file '{oldPath}' to SQLite format is about to begin. Please wait a moment.");

			// ToDo: Temporary to fix https://github.com/zkSNACKs/WalletWasabi/pull/12137#issuecomment-1879798750
			NeedResync = File.Exists(dbPath);

			string[] allLines = File.ReadAllLines(oldPath, Encoding.UTF8);
			IEnumerable<SmartTransaction> allTransactions = allLines.Select(x => SmartTransaction.FromLine(x, network));

			SqliteStorage.BulkInsert(allTransactions);

			Logger.LogInfo($"Removing old '{oldPath}' transaction storage.");
			File.Delete(oldPath);
		}
	}

	public Task InitializeAsync(string operationName, CancellationToken cancellationToken)
	{
		using (BenchmarkLogger.Measure(operationName: operationName))
		{
			lock (SqliteStorageLock)
			{
				InitializeTransactionsNoLock(cancellationToken);
			}
		}

		return Task.CompletedTask;
	}

	private void InitializeTransactionsNoLock(CancellationToken cancellationToken)
	{
		try
		{
			lock (SqliteStorageLock)
			{
				int i = 0;
				foreach (SmartTransaction tx in SqliteStorage.GetAll(cancellationToken).OrderByBlockchain())
				{
					i++;

					if (i % 100 == 0)
					{
						cancellationToken.ThrowIfCancellationRequested();
					}

					_ = TryAddOrUpdateNoLockNoSerialization(tx);
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// We found a corrupted entry. Stop here.
			// Do not try to automatically correct the data, because the internal data structures are throwing events that may confuse the consumers of those events.
			Logger.LogError($"'{DataSource}' database got corrupted. Clearing it...");
			SqliteStorage.Clear();
			throw;
		}
	}

	private bool TryAddOrUpdateNoLockNoSerialization(SmartTransaction tx)
	{
		uint256 hash = tx.GetHash();

		if (Transactions.TryAdd(hash, tx))
		{
			return true;
		}
		else
		{
			if (Transactions[hash].TryUpdate(tx))
			{
				return true;
			}
		}

		return false;
	}

	public bool TryAdd(SmartTransaction tx)
	{
		lock (SqliteStorageLock)
		{
			if (Transactions.TryAdd(tx.GetHash(), tx))
			{
				if (SqliteStorage.BulkInsert(tx) == 0)
				{
					throw new UnreachableException($"Transaction '{tx.GetHash()}' was added in memory but not in database.");
				}

				return true;
			}

			return false;
		}
	}

	public bool TryAddOrUpdate(SmartTransaction tx)
	{
		lock (SqliteStorageLock)
		{
			bool result = TryAddOrUpdateNoLockNoSerialization(tx);

			if (result)
			{
				if (SqliteStorage.BulkInsert(new SmartTransaction[] { tx }, upsert: true) == 0)
				{
					throw new UnreachableException($"Transaction '{tx.GetHash()}' was update in memory but not in database.");
				}
			}

			return result;
		}
	}

	public bool TryUpdate(SmartTransaction tx)
	{
		bool updated = false;

		lock (SqliteStorageLock)
		{
			if (Transactions.TryGetValue(tx.GetHash(), out var foundTx))
			{
				if (foundTx.TryUpdate(tx))
				{
					updated = true;

					if (SqliteStorage.BulkUpdate(tx) == 0)
					{
						throw new UnreachableException($"Transaction '{tx.GetHash()}' was update in memory but not in database.");
					}
				}
			}
		}

		return updated;
	}

	public bool TryRemove(uint256 hash, [NotNullWhen(true)] out SmartTransaction? tx)
	{
		bool isRemoved = false;

		lock (SqliteStorageLock)
		{
			if (Transactions.Remove(hash, out tx))
			{
				isRemoved = true;

				if (!SqliteStorage.TryRemove(hash, out _))
				{
					throw new UnreachableException($"Transaction '{tx.GetHash()}' was removed from memory but not from database.");
				}
			}
		}

		return isRemoved;
	}

	public bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? tx)
	{
		lock (SqliteStorageLock)
		{
			return Transactions.TryGetValue(hash, out tx);
		}
	}

	public List<SmartTransaction> GetTransactions()
	{
		lock (SqliteStorageLock)
		{
			return Transactions.Values.OrderByBlockchain().ToList();
		}
	}

	public List<uint256> GetTransactionHashes()
	{
		lock (SqliteStorageLock)
		{
			return Transactions.Values.OrderByBlockchain().Select(x => x.GetHash()).ToList();
		}
	}

	public bool IsEmpty()
	{
		lock (SqliteStorageLock)
		{
			return Transactions.Count == 0;
		}
	}

	public bool Contains(uint256 hash)
	{
		lock (SqliteStorageLock)
		{
			return Transactions.ContainsKey(hash);
		}
	}

	public ValueTask DisposeAsync()
	{
		SqliteStorage.Dispose();

		return ValueTask.CompletedTask;
	}
}
