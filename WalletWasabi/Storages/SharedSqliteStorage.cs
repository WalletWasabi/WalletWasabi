using Microsoft.Data.Sqlite;

namespace WalletWasabi.Storages;

public delegate SqliteConnection SqliteConnectionFactory();

/// <summary>
/// SQLite-based storage for public data such Bitcoin block filters, P2P seed data, etc. which are not related to any particular wallet.
/// </summary>
public class SharedSqliteStorage : IDisposable
{
	private readonly string _connectionString;
	private bool _disposedValue;

	private SharedSqliteStorage(string connectionString)
	{
		_connectionString = connectionString;
	}

	public SqliteConnectionFactory GetConnectionFactory() => CreateConnection;

	/// <summary>
	/// Opens a new SQLite connection to the given database file.
	/// </summary>
	/// <param name="filePath">Path to the SQLite database file.</param>
	/// <param name="startingFilter">Starting filter to put into the filter table if the table needs to be created.</param>
	/// <exception cref="InvalidOperationException">If there is an unrecoverable error.</exception>
	/// <seealso href="https://dev.to/lefebvre/speed-up-sqlite-with-write-ahead-logging-wal-do">Write-ahead logging explained.</seealso>
	public static SharedSqliteStorage FromFile(string filePath)
	{
		SqliteConnectionStringBuilder builder = new();
		builder.DataSource = filePath;
		builder.Pooling = true;

		string connectionString = builder.ConnectionString;

		using var connection = new SqliteConnection(connectionString);
		connection.Open();

		// Table for compact block filters (BIP 157).
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

		// Table for P2P seed nodes.
		using (var createCommand = connection.CreateCommand())
		{
			createCommand.CommandText = """
				CREATE TABLE IF NOT EXISTS p2p_seed_node (
				    node TEXT NOT NULL PRIMARY KEY
				);
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
		return connection;
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
