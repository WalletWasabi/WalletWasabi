using Microsoft.Data.Sqlite;
using System.IO;

namespace WalletWasabi.Storages;

public delegate SqliteConnection SqliteConnectionFactory();

/// <summary>
/// SQLite-based storage for public data such as Bitcoin block filters, P2P seed data, etc. which are not related to any particular wallet.
/// </summary>
public class SharedSqliteStorage : IDisposable
{
	private const int CurrentUserVersion = 2;

	private readonly string _connectionString;
	private bool _disposedValue;

	private SharedSqliteStorage(string connectionString)
	{
		_connectionString = connectionString;
	}

	public SqliteConnectionFactory GetConnectionFactory() => CreateConnection;

	public static SharedSqliteStorage FromFile(string filePath)
	{
		IoHelpers.EnsureContainingDirectoryExists(filePath);

		var dbExists = File.Exists(filePath);

		if (dbExists)
		{
			try
			{
				var storage = Open(filePath);

				if (storage.GetUserVersion() >= CurrentUserVersion)
				{
					return storage;
				}

				storage.Dispose();
				Logger.LogInfo("Migrating from old Indexer filters to Bitcoin Core RPC filters.");
			}
			catch (SqliteException ex) when (ex.SqliteErrorCode is 11 or 26) // 11 ~ SQLITE_CORRUPT error code, 26 ~ SQLITE_NOTADB error code
			{
				Logger.LogError($"SQLite storage file '{filePath}' is corrupted (code {ex.SqliteExtendedErrorCode}). Deleting it.");
				SqliteConnection.ClearAllPools();
			}

			SqliteStorageHelper.DeleteDatabaseFiles(filePath);
		}

		// Create a new database file and initialize it.
		var newStorage = Open(filePath);
		newStorage.SetUserVersion(CurrentUserVersion);
		return newStorage;
	}

	/// <summary>
	/// Opens a new SQLite connection to the given database file, if it exists or not.
	/// </summary>
	/// <seealso href="https://dev.to/lefebvre/speed-up-sqlite-with-write-ahead-logging-wal-do">Write-ahead logging explained.</seealso>
	private static SharedSqliteStorage Open(string filePath)
	{
		SqliteConnectionStringBuilder builder = new();
		builder.DataSource = filePath;
		builder.Pooling = true;

		string connectionString = builder.ConnectionString;

		using var connection = new SqliteConnection(connectionString);
		connection.Open();

		using (var createCommand = connection.CreateCommand())
		{
			createCommand.CommandText = """
				CREATE TABLE IF NOT EXISTS filter (
				    block_height INTEGER NOT NULL PRIMARY KEY,
				    block_hash BLOB NOT NULL,
				    filter_data BLOB NOT NULL,
				    previous_block_hash BLOB NOT NULL,
				    epoch_block_time INTEGER NOT NULL
				);
				CREATE INDEX IF NOT EXISTS idx_blocks_height ON filter(block_height);
				CREATE INDEX IF NOT EXISTS idx_blocks_hash ON filter(block_hash);
				""";
			createCommand.ExecuteNonQuery();
		}

		// Enable write-ahead logging.
		using (var walCommand = connection.CreateCommand())
		{
			walCommand.CommandText = """
				PRAGMA journal_mode = 'wal';
				PRAGMA synchronous  = 'NORMAL';
				""";
			walCommand.ExecuteNonQuery();
		}

		return new(connectionString);
	}

	/// <summary>
	/// Creates and opens a new pooled connection.
	/// </summary>
	private SqliteConnection CreateConnection()
	{
		var connection = new SqliteConnection(_connectionString);
		connection.Open();

		using var command = connection.CreateCommand();
		command.CommandText = "PRAGMA synchronous = NORMAL;";
		command.ExecuteNonQuery();

		return connection;
	}

	public int GetUserVersion()
	{
		using var connection = CreateConnection();
		using var command = connection.CreateCommand();
		command.CommandText = "PRAGMA user_version";

		return Convert.ToInt32(command.ExecuteScalar());
	}

	public void SetUserVersion(int newUserVersion)
	{
		using var connection = CreateConnection();
		using var command = connection.CreateCommand();
		command.CommandText = $"PRAGMA user_version = {newUserVersion};";
		command.ExecuteNonQuery();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				// Empty the connection pool to release all connections and make sure the database is no longer in use.
				SqliteConnection.ClearAllPools();
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
