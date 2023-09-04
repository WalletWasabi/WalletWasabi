using Microsoft.Data.Sqlite;
using NBitcoin;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Stores;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionStore : IAsyncDisposable
{
	public TransactionStore(string workFolderPath, Network network, bool migrateData = true)
	{
		workFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
		IoHelpers.EnsureDirectoryExists(workFolderPath);

		string dataSource;

		if (workFolderPath == SqliteStorageHelper.InMemoryDatabase)
		{
			dataSource = SqliteStorageHelper.InMemoryDatabase;
		}
		else
		{
			IoHelpers.EnsureDirectoryExists(workFolderPath);
			dataSource = Path.Combine(workFolderPath, "Transactions.sqlite");

			// TODO: Remove. Useful for testing.
			// if (File.Exists(dataSource))
			// {
			// 	 File.Delete(dataSource);
			// }
		}

		SqliteStorage = TransactionSqliteStorage.FromFile(dataSource: dataSource, network);

		if (migrateData)
		{
			string oldPath = Path.Combine(workFolderPath, "Transactions.dat");
			Import(oldPath, network, deleteAfterImport: false);
		}
	}

	private TransactionSqliteStorage SqliteStorage { get; }
	private object SqliteStorageLock { get; } = new();

	private void Import(string oldPath, Network network, bool deleteAfterImport = false)
	{
		if (File.Exists(oldPath))
		{
			SqliteStorage.Clear();

			IoManager transactionsFileManager = new(filePath: oldPath);

			string[] allLines = File.ReadAllLines(oldPath, Encoding.UTF8);
			IEnumerable<SmartTransaction> allTransactions = allLines.Select(x => SmartTransaction.FromLine(x, network));

			SqliteStorage.BulkInsert(allTransactions);

			if (deleteAfterImport)
			{
				Logger.LogInfo($"Removing old '{oldPath}' transaction storage.");
				File.Delete(oldPath);
			}
		}
	}

	public bool TryAdd(SmartTransaction tx)
	{
		lock (SqliteStorageLock)
		{
			int result = BulkInsert(tx);
			return result > 0;
		}
	}

	public bool TryAddOrUpdate(SmartTransaction tx)
	{
		lock (SqliteStorageLock)
		{
			int result = BulkUpdate(tx);
			return result > 0;
		}
	}

	public bool TryUpdate(SmartTransaction tx)
	{
		lock (SqliteStorageLock)
		{
			int result = BulkUpdate(tx);
			return result > 0;
		}
	}

	public bool TryRemove(uint256 hash, [NotNullWhen(true)] out SmartTransaction? tx)
	{
		lock (SqliteStorageLock)
		{
			return SqliteStorage.TryRemove(hash, out tx);
		}
	}

	public bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? tx)
	{
		lock (SqliteStorageLock)
		{
			return SqliteStorage.TryGet(hash, out tx);
		}
	}

	public List<SmartTransaction> GetTransactions()
	{
		lock (SqliteStorageLock)
		{
			return SqliteStorage.GetAll().ToList();
		}
	}

	public List<uint256> GetTransactionHashes()
	{
		lock (SqliteStorageLock)
		{
			return SqliteStorage.GetAllTxids().ToList();
		}
	}

	public bool IsEmpty()
	{
		lock (SqliteStorageLock)
		{
			return SqliteStorage.IsEmpty();
		}
	}

	public bool Contains(uint256 hash)
	{
		lock (SqliteStorageLock)
		{
			return SqliteStorage.Contains(txid: hash);
		}
	}

	private int BulkInsert(params SmartTransaction[] transactions)
		=> BulkInsert(transactions as IEnumerable<SmartTransaction>);

	private int BulkInsert(IEnumerable<SmartTransaction> transactions)
	{
		try
		{
			lock (SqliteStorageLock)
			{
				return SqliteStorage.BulkInsert(transactions.OrderByBlockchain());
			}
		}
		catch (SqliteException ex)
		{
			Logger.LogError(ex);
			throw;
		}
	}

	private void BulkDelete(params uint256[] transactionIds)
		=> BulkDelete(transactionIds as IReadOnlyList<uint256>);

	private void BulkDelete(IReadOnlyList<uint256> transactionIds)
	{
		try
		{
			lock (SqliteStorageLock)
			{
				SqliteStorage.BulkRemove(transactionIds);
			}
		}
		catch (SqliteException ex)
		{
			Logger.LogError(ex);
			throw;
		}
	}

	/// <inheritdoc cref="BulkUpdate(IEnumerable{SmartTransaction})"/>
	private int BulkUpdate(params SmartTransaction[] transactions)
		=> BulkUpdate(transactions as IEnumerable<SmartTransaction>);

	/// <returns>Number of modified rows.</returns>
	/// <exception cref="SqliteException">If there is an issue with the operation.</exception>
	private int BulkUpdate(IEnumerable<SmartTransaction> transactions)
	{
		try
		{
			lock (SqliteStorageLock)
			{
				return SqliteStorage.BulkUpdate(transactions);
			}
		}
		catch (SqliteException ex)
		{
			Logger.LogError(ex);
			throw;
		}
	}

	public ValueTask DisposeAsync()
	{
		SqliteStorage.Dispose();

		return ValueTask.CompletedTask;
	}
}
