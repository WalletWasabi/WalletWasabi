using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Stores;
using static WalletWasabi.Stores.TransactionSqliteStorage;

namespace WalletWasabi.Blockchain.Transactions;

public class AllTransactionStore : ITransactionStore, IAsyncDisposable
{
	public AllTransactionStore(string workFolderPath, Network network, bool migrateData = true)
	{
		string dataSource;

		if (workFolderPath == SqliteStorageHelper.InMemoryDatabase)
		{
			dataSource = SqliteStorageHelper.InMemoryDatabase;
		}
		else
		{
			IoHelpers.EnsureDirectoryExists(workFolderPath);
			dataSource = Path.Combine(workFolderPath, "Storage.sqlite");

			// TODO: Remove. Useful for testing.
			if (File.Exists(dataSource))
			{
				File.Delete(dataSource);
			}
		}

		SqliteStorage = TransactionSqliteStorage.FromFile(dataSource: dataSource, network);

		if (migrateData)
		{
			string oldPath1 = Path.Combine(Path.Combine(workFolderPath, "Mempool"), "Transactions.dat");
			string oldPath2 = Path.Combine(Path.Combine(workFolderPath, "ConfirmedTransactions", Constants.ConfirmedTransactionsVersion), "Transactions.dat");

			Import(oldPath1, network, deleteAfterImport: false);
			Import(oldPath2, network, deleteAfterImport: false);
		}
	}

	private TransactionSqliteStorage SqliteStorage { get; }

	private object Lock { get; } = new();

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

	public virtual bool TryGetMempoolTransaction(uint256 txid, [NotNullWhen(true)] out SmartTransaction? sameStx)
	{
		lock (Lock)
		{
			if (SqliteStorage.TryGet(txid, out SmartTransaction? foundTx))
			{
				if (!foundTx.Confirmed)
				{
					sameStx = foundTx;
					return true;
				}
			}

			sameStx = null;
			return false;
		}
	}

	public virtual bool TryGetConfirmedTransaction(uint256 txid, [NotNullWhen(true)] out SmartTransaction? sameStx)
	{
		lock (Lock)
		{
			if (SqliteStorage.TryGet(txid, out SmartTransaction? foundTx))
			{
				if (foundTx.Confirmed)
				{
					sameStx = foundTx;
					return true;
				}
			}

			sameStx = null;
			return false;
		}
	}

	public virtual bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? sameStx)
	{
		lock (Lock)
		{
			return SqliteStorage.TryGet(hash, out sameStx);
		}
	}

	public void AddOrUpdate(SmartTransaction tx)
	{
		lock (Lock)
		{
			SqliteStorage.BulkInsert(new SmartTransaction[] { tx }, upsert: true);
		}
	}

	public bool TryUpdate(SmartTransaction tx)
	{
		lock (Lock)
		{
			return TryUpdateNoLock(tx);
		}
	}

	/// <remarks>Method requires <see cref="Lock"/> acquired.</remarks>
	private bool TryUpdateNoLock(SmartTransaction tx)
	{
		int result = SqliteStorage.BulkUpdate(tx);
		return result > 0;
	}

	public bool TryRemove(TransactionType type, uint256 hash, [NotNullWhen(true)] out SmartTransaction? tx)
	{
		lock (Lock)
		{
			return SqliteStorage.TryRemove(type, hash, out tx);
		}
	}

	/// <inheritdoc cref="GetTransactionsNoLock"/>
	public IEnumerable<SmartTransaction> GetTransactions(TransactionType type = TransactionType.All)
	{
		lock (Lock)
		{
			return GetTransactionsNoLock(type);
		}
	}

	public IEnumerable<SmartTransaction> GetMempoolTransactions()
		=> GetTransactions(TransactionType.Mempool);

	public IEnumerable<SmartTransaction> GetConfirmedTransactions()
		=> GetTransactions(TransactionType.Confirmed);

	/// <returns>Transactions are ordered by blockchain.</returns>
	private IEnumerable<SmartTransaction> GetTransactionsNoLock(TransactionType type = TransactionType.All)
	{
		return SqliteStorage.GetAll(type).ToList();
	}

	public IEnumerable<uint256> GetTransactionHashes(TransactionType type = TransactionType.All)
	{
		lock (Lock)
		{
			return SqliteStorage.GetAllTxids(type).ToList();
		}
	}

	public bool IsEmpty(TransactionType type = TransactionType.All)
	{
		lock (Lock)
		{
			return SqliteStorage.IsEmpty(type);
		}
	}

	/// <remarks>Only used by tests.</remarks>
	internal bool Contains(uint256 txid)
	{
		lock (Lock)
		{
			return SqliteStorage.Contains(txid: txid);
		}
	}

	public IReadOnlyList<SmartTransaction> ReleaseToMempoolFromBlock(uint256 blockHash)
	{
		lock (Lock)
		{
			List<SmartTransaction> reorgedTxs = new();

			foreach (SmartTransaction tx in GetTransactionsNoLock().Where(tx => tx.BlockHash == blockHash))
			{
				tx.SetUnconfirmed();

				if (TryUpdateNoLock(tx))
				{
					reorgedTxs.Add(tx);
				}
			}

			return reorgedTxs;
		}
	}

	/// <returns>Labels ordered by blockchain.</returns>
	public IEnumerable<LabelsArray> GetLabels() => GetTransactions().Select(x => x.Labels);

	public ValueTask DisposeAsync()
	{
		SqliteStorage.Dispose();
		return ValueTask.CompletedTask;
	}
}
