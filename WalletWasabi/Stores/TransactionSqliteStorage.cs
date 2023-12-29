using Microsoft.Data.Sqlite;
using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Stores;

public class TransactionSqliteStorage : IDisposable
{
	private const string AllColumns = "txid, block_height, block_hash, block_index, labels, first_seen, is_replacement, is_speedup, is_cancellation, tx";
	private const string AllParameters = "$txid, $block_height, $block_hash, $block_index, $labels, $first_seen, $is_replacement, $is_speedup, $is_cancellation, $tx";
	private bool _disposedValue;

	private TransactionSqliteStorage(SqliteConnection connection, Network network)
	{
		Connection = connection;
		Network = network;
	}

	/// <remarks>Connection cannot be accessed from multiple threads at the same time.</remarks>
	private SqliteConnection Connection { get; }
	private Network Network { get; }

	/// <summary>
	/// Opens a new SQLite connection to the given database file.
	/// </summary>
	/// <param name="dataSource">Path to the SQLite database file, or special <c>:memory:</c> string.</param>
	/// <seealso href="https://dev.to/lefebvre/speed-up-sqlite-with-write-ahead-logging-wal-do">Write-ahead logging explained.</seealso>
	/// <exception cref="SqliteException">If there is an issue with the operation.</exception>
	public static TransactionSqliteStorage FromFile(string dataSource, Network network)
	{
		// In case there is an exception, we need to dispose things properly.
		SqliteConnection? connectionToDispose = null;
		TransactionSqliteStorage? storageToDispose = null;

		try
		{
			SqliteConnectionStringBuilder builder = new();
			builder.DataSource = dataSource;

			SqliteConnection connection = new(builder.ConnectionString);
			connectionToDispose = connection;
			connection.Open();

			using (SqliteCommand command = connection.CreateCommand())
			{
				command.CommandText = $$"""
					CREATE TABLE IF NOT EXISTS "transaction" (
						txid BLOB NOT NULL PRIMARY KEY, /* bytes are in little endian; we consider TXIDs to be unique */
						block_height INTEGER NOT NULL, /* mempool tx ~ Int32.MaxValue-1; unknown height ~ Int32.MaxValue; otherwise a valid block height */
						block_hash BLOB, /* NULL ~ not in a block yet. If NOT NULL then bytes are in little endian. */
						block_index INTEGER NOT NULL,
						labels TEXT NOT NULL, /* empty string ~ no labels */
						first_seen INTEGER NOT NULL, /* UNIX timestamp in seconds */
						is_replacement BOOLEAN NOT NULL,
						is_speedup BOOLEAN NOT NULL,
						is_cancellation BOOLEAN NOT NULL,
						tx BLOB NOT NULL /* transaction as a binary array */
					);
					CREATE INDEX IF NOT EXISTS transaction_blockchain_idx ON "transaction" (block_height, block_index, first_seen);
					""";
				command.ExecuteNonQuery();
			}

			// Enable write-ahead logging.
			using (SqliteCommand walCommand = connection.CreateCommand())
			{
				walCommand.CommandText = """
					PRAGMA journal_mode = 'wal';
					PRAGMA synchronous  = 'NORMAL';
					""";
				walCommand.ExecuteNonQuery();
			}

			TransactionSqliteStorage storage = new(connection, network);
			storageToDispose = storage;
			connectionToDispose = null;

			storageToDispose = null;
			connectionToDispose = null;

			return storage;
		}
		finally
		{
			storageToDispose?.Dispose();
			connectionToDispose?.Close();
			connectionToDispose?.Dispose();
		}
	}

	/// <summary>
	/// Insert/upsert transactions in bulk to the table.
	/// </summary>
	/// <param name="transactions">Transactions to store in the SQLite database.</param>
	/// <param name="upsert">Whether transactions are to be updated if they exist or not.</param>
	/// <returns>Number of affected rows.</returns>
	/// <remarks>
	/// Notably the method does not throw <see cref="SqliteException"/> if <paramref name="transactions"/> contains an already-stored transaction.
	/// </remarks>
	/// <exception cref="SqliteException">If there is an issue with the operation.</exception>
	public int BulkInsert(IEnumerable<SmartTransaction> transactions, bool upsert = false)
	{
		using SqliteTransaction transaction = Connection.BeginTransaction();
		using SqliteCommand command = Connection.CreateCommand();

		string commandText;

		if (upsert)
		{
			commandText = $"""
				INSERT INTO "transaction" ({AllColumns})
				VALUES ({AllParameters})
				ON CONFLICT(txid) DO UPDATE SET
					block_height=excluded.block_height,
					block_hash=excluded.block_hash,
					block_index=excluded.block_index,
					labels=excluded.labels,
					first_seen=excluded.first_seen,
					is_replacement=excluded.is_replacement,
					is_speedup=excluded.is_speedup,
					is_cancellation=excluded.is_cancellation
				""";
		}
		else
		{
			commandText = $"""
				INSERT OR IGNORE INTO "transaction" ({AllColumns})
				VALUES ({AllParameters})
				""";
		}

		command.CommandText = commandText;

		SqliteParameter txidParameter = CreateParameter(command, "$txid");
		command.Parameters.Add(txidParameter);

		SqliteParameter blockHeightParameter = CreateParameter(command, "$block_height");
		command.Parameters.Add(blockHeightParameter);

		SqliteParameter blockHashParameter = CreateParameter(command, "$block_hash");
		command.Parameters.Add(blockHashParameter);

		SqliteParameter blockIndexParameter = CreateParameter(command, "$block_index");
		command.Parameters.Add(blockIndexParameter);

		SqliteParameter labelsParameter = CreateParameter(command, "$labels");
		command.Parameters.Add(labelsParameter);

		SqliteParameter firstSeenParameter = CreateParameter(command, "$first_seen");
		command.Parameters.Add(firstSeenParameter);

		SqliteParameter isReplacementParameter = CreateParameter(command, "$is_replacement");
		command.Parameters.Add(isReplacementParameter);

		SqliteParameter isSpeedupParameter = CreateParameter(command, "$is_speedup");
		command.Parameters.Add(isSpeedupParameter);

		SqliteParameter isCancellationParameter = CreateParameter(command, "$is_cancellation");
		command.Parameters.Add(isCancellationParameter);

		SqliteParameter txParameter = CreateParameter(command, "$tx");
		command.Parameters.Add(txParameter);

		int changedRows = 0;

		foreach (SmartTransaction tx in transactions)
		{
			txidParameter.Value = tx.GetHash().ToBytes(lendian: true);
			blockHeightParameter.Value = tx.Height.Value;

			byte[]? blockHash = tx.BlockHash?.ToBytes(lendian: true);
			blockHashParameter.Value = (blockHash is not null) ? blockHash : DBNull.Value;

			blockIndexParameter.Value = tx.BlockIndex;
			labelsParameter.Value = tx.Labels.ToString();
			firstSeenParameter.Value = tx.FirstSeen.ToUnixTimeSeconds();
			isReplacementParameter.Value = tx.IsReplacement ? 1 : 0;
			isSpeedupParameter.Value = tx.IsSpeedup ? 1 : 0;
			isCancellationParameter.Value = tx.IsCancellation ? 1 : 0;
			txParameter.Value = tx.Transaction.ToBytes();

			int affectedRows = command.ExecuteNonQuery();

			if (affectedRows > -1)
			{
				changedRows += affectedRows;
			}
		}

		transaction.Commit();

		return changedRows;
	}

	/// <summary>
	/// Simple factory of <see cref="SqliteParameter"/>.
	/// </summary>
	private SqliteParameter CreateParameter(SqliteCommand command, string parameterName)
	{
		SqliteParameter parameter = command.CreateParameter();
		parameter.ParameterName = parameterName;
		return parameter;
	}

	/// <inheritdoc cref="BulkInsert(IEnumerable{SmartTransaction}, bool)"/>
	public int BulkInsert(params SmartTransaction[] transactions)
		=> BulkInsert(transactions as IEnumerable<SmartTransaction>);

	/// <summary>
	/// Update transactions in bulk.
	/// </summary>
	/// <param name="transactions">Transactions to update in the SQLite database.</param>
	/// <returns>Number of affected rows.</returns>
	/// <exception cref="SqliteException">If there is an issue with the operation.</exception>
	public int BulkUpdate(IEnumerable<SmartTransaction> transactions)
	{
		using SqliteTransaction transaction = Connection.BeginTransaction();
		using SqliteCommand command = Connection.CreateCommand();

		string commandText = """
			UPDATE "transaction"
			SET
				block_height = $block_height,
				block_hash = $block_hash,
				block_index = $block_index,
				labels = $labels,
				first_seen = $first_seen,
				is_replacement = $is_replacement,
				is_speedup = $is_speedup,
				is_cancellation = $is_cancellation
			WHERE txid = $txid
			""";

		command.CommandText = commandText;

		SqliteParameter txidParameter = CreateParameter(command, "$txid");
		command.Parameters.Add(txidParameter);

		SqliteParameter blockHeightParameter = CreateParameter(command, "$block_height");
		command.Parameters.Add(blockHeightParameter);

		SqliteParameter blockHashParameter = CreateParameter(command, "$block_hash");
		command.Parameters.Add(blockHashParameter);

		SqliteParameter blockIndexParameter = CreateParameter(command, "$block_index");
		command.Parameters.Add(blockIndexParameter);

		SqliteParameter labelsParameter = CreateParameter(command, "$labels");
		command.Parameters.Add(labelsParameter);

		SqliteParameter firstSeenParameter = CreateParameter(command, "$first_seen");
		command.Parameters.Add(firstSeenParameter);

		SqliteParameter isReplacementParameter = CreateParameter(command, "$is_replacement");
		command.Parameters.Add(isReplacementParameter);

		SqliteParameter isSpeedupParameter = CreateParameter(command, "$is_speedup");
		command.Parameters.Add(isSpeedupParameter);

		SqliteParameter isCancellationParameter = CreateParameter(command, "$is_cancellation");
		command.Parameters.Add(isCancellationParameter);

		int changedRows = 0;

		foreach (SmartTransaction tx in transactions)
		{
			txidParameter.Value = tx.GetHash().ToBytes(lendian: true);
			blockHeightParameter.Value = tx.Height.Value;

			byte[]? blockHash = tx.BlockHash?.ToBytes(lendian: true);
			blockHashParameter.Value = (blockHash is not null) ? blockHash : DBNull.Value;

			blockIndexParameter.Value = tx.BlockIndex;
			labelsParameter.Value = tx.Labels.ToString();
			firstSeenParameter.Value = tx.FirstSeen.ToUnixTimeSeconds();
			isReplacementParameter.Value = tx.IsReplacement ? 1 : 0;
			isSpeedupParameter.Value = tx.IsSpeedup ? 1 : 0;
			isCancellationParameter.Value = tx.IsCancellation ? 1 : 0;

			int affectedRows = command.ExecuteNonQuery();

			if (affectedRows > -1)
			{
				changedRows += affectedRows;
			}
		}

		transaction.Commit();

		return changedRows;
	}

	/// <inheritdoc cref="BulkUpdate(IEnumerable{SmartTransaction})"/>
	public int BulkUpdate(params SmartTransaction[] transactions)
		=> BulkUpdate(transactions as IEnumerable<SmartTransaction>);

	/// <summary>
	/// Append transactions in bulk to the table.
	/// </summary>
	/// <param name="txids">Transactions to remove from the SQLite database.</param>
	/// <returns>Number of deleted records.</returns>
	/// <exception cref="SqliteException">If there is an issue with the operation.</exception>
	public int BulkRemove(IReadOnlyList<uint256> txids)
	{
		using SqliteTransaction transaction = Connection.BeginTransaction();
		using SqliteCommand command = Connection.CreateCommand();

		string inParameters = string.Join(",", Enumerable.Range(0, txids.Count).Select(z => "@para" + z));
		command.CommandText = $$"""DELETE FROM "transaction" WHERE txid IN ({{inParameters}})""";

		// Bind all parameters.
		for (int i = 0; i < txids.Count; i++)
		{
			command.Parameters.Add(new SqliteParameter("@para" + i, txids[i].ToBytes(lendian: true)));
		}

		int deleted = command.ExecuteNonQuery();
		transaction.Commit();

		return deleted;
	}

	public bool TryGet(uint256 txid, [NotNullWhen(true)] out SmartTransaction? tx)
	{
		string query = $$"""
			SELECT {{AllColumns}}
			FROM "transaction"
			WHERE txid = $txid
			""";

		return TryReadSingleRecord(txid, query, out tx);
	}

	public bool TryRemove(uint256 txid, out SmartTransaction? tx)
	{
		using SqliteTransaction transaction = Connection.BeginTransaction();
		bool result = TryReadSingleRecord(txid, $$"""DELETE FROM "transaction" WHERE txid = $txid returning *""", out tx);
		transaction.Commit();

		return result;
	}

	private bool TryReadSingleRecord(uint256 hash, string query, out SmartTransaction? tx)
	{
		using SqliteCommand command = Connection.CreateCommand();

		command.CommandText = query;

		SqliteParameter txidParameter = command.CreateParameter();
		txidParameter.ParameterName = "$txid";
		command.Parameters.Add(txidParameter);

		txidParameter.Value = hash.ToBytes(lendian: true);

		using SqliteDataReader reader = command.ExecuteReader();

		if (!reader.Read())
		{
			tx = null;
			return false;
		}

		tx = ReadRow(reader);
		return true;
	}

	public IEnumerable<SmartTransaction> GetAll(CancellationToken cancellationToken)
	{
		using SqliteTransaction transaction = Connection.BeginTransaction();
		using SqliteCommand command = Connection.CreateCommand();

		// Note that a transaction can be in the mempool, so it does not have any specific block height assigned. However, we assign such 
		// transactions Int32.MaxValue-1 height and as such mempool transactions would be returned first.
		command.CommandText = $$"""
			SELECT {{AllColumns}}
			FROM "transaction"
			ORDER BY block_height, block_index, first_seen
			""";

		using SqliteDataReader reader = command.ExecuteReader();

		int i = 0;
		while (reader.Read())
		{
			i++;

			if (i % 100 == 0)
			{
				cancellationToken.ThrowIfCancellationRequested();
			}

			SmartTransaction filter = ReadRow(reader);
			yield return filter;
		}
	}

	public IEnumerable<uint256> GetAllTxids()
	{
		using SqliteTransaction transaction = Connection.BeginTransaction();
		using SqliteCommand command = Connection.CreateCommand();

		command.CommandText = $$"""
			SELECT txid
			FROM "transaction"
			ORDER BY block_height, block_index, first_seen
			""";

		using SqliteDataReader reader = command.ExecuteReader();

		while (reader.Read())
		{
			uint256 txid = new(reader.GetFieldValue<byte[]>(ordinal: 0));
			yield return txid;
		}
	}

	private SmartTransaction ReadRow(SqliteDataReader reader)
	{
		uint256 txid = new(reader.GetFieldValue<byte[]>(ordinal: 0));
		int blockHeight = reader.GetInt32(ordinal: 1);
		uint256? blockHash = reader.IsDBNull(ordinal: 2) ? null : new(reader.GetFieldValue<byte[]>(ordinal: 2));
		int blockIndex = reader.GetInt32(ordinal: 3);
		string labelsString = reader.GetString(ordinal: 4);
		long firstSeenLong = reader.GetInt64(ordinal: 5);
		bool isReplacement = reader.GetInt32(ordinal: 6) == 1;
		bool isSpeedup = reader.GetInt32(ordinal: 7) == 1;
		bool isCancellation = reader.GetInt32(ordinal: 8) == 1;
		byte[] tx = reader.GetFieldValue<byte[]>(ordinal: 9);

		Transaction transaction = Transaction.Load(tx, Network);

		Height height = new(blockHeight);
		LabelsArray labelsArray = new(labelsString);
		DateTimeOffset firstSeen = DateTimeOffset.FromUnixTimeSeconds(firstSeenLong);

		SmartTransaction stx = new(transaction, height, blockHash, blockIndex, labelsArray, isReplacement, isSpeedup, isCancellation, firstSeen);

		if (stx.GetHash() != txid)
		{
			throw new InvalidOperationException();
		}

		return stx;
	}

	/// <returns>Returns <c>true</c> if there are no records in the transactions table.</returns>
	public bool IsEmpty()
	{
		using SqliteCommand command = Connection.CreateCommand();
		command.CommandText = """SELECT EXISTS (SELECT 1 FROM "transaction")""";
		object? result = command.ExecuteScalar();

		if (result is not long exists)
		{
			Logger.LogError($"Unexpected result returned: '{result}'.");
			throw new InvalidOperationException("Unexpected result returned.");
		}

		return exists == 0;
	}

	/// <returns>Returns <c>true</c> if there are no records in the transactions table.</returns>
	public bool Contains(uint256 txid)
	{
		using SqliteCommand command = Connection.CreateCommand();
		command.CommandText = """SELECT EXISTS(SELECT 1 FROM "transaction" WHERE txid=$txid);""";

		SqliteParameter txidParameter = command.CreateParameter();
		txidParameter.ParameterName = "$txid";
		command.Parameters.Add(txidParameter);

		txidParameter.Value = txid.ToBytes(lendian: true);

		object? result = command.ExecuteScalar();

		if (result is not long contains)
		{
			Logger.LogError($"Unexpected result returned: '{result}'.");
			throw new InvalidOperationException("Unexpected result returned.");
		}

		return contains == 1;
	}

	/// <summary>
	/// Clears all transactions from the SQLite table.
	/// </summary>
	/// <returns><c>true</c> if at least one row was deleted, <c>false</c> otherwise.</returns>
	public bool Clear()
	{
		using SqliteCommand command = Connection.CreateCommand();
		command.CommandText = """DELETE FROM "transaction";""";
		int affectedLines = command.ExecuteNonQuery();

		return affectedLines > 0;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				Connection.Close();
				Connection.Dispose();
			}

			_disposedValue = true;
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
