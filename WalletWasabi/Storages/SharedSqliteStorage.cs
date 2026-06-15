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
		CreateCompactBlockFilterTableIfMissing(connection);

		// Table for P2P seed nodes.
		CreateP2pSeedNodeTableIfMissing(connection);

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

	private static void CreateP2pSeedNodeTableIfMissing(SqliteConnection connection)
	{
		using (var createCommand = connection.CreateCommand())
		{
			createCommand.CommandText = """
				CREATE TABLE IF NOT EXISTS p2p_seed_node (
				    node TEXT NOT NULL PRIMARY KEY
				);
				""";
			createCommand.ExecuteNonQuery();
		}

		// Check if the table requires first-time initialization.
		using var checkCommand = connection.CreateCommand();
		checkCommand.CommandText = "SELECT COUNT(*) FROM p2p_seed_node";
		var count = (long?)checkCommand.ExecuteScalar();

		if (count == 0)
		{
			var initialNodes = new[]
			{
				"[::ffff:89.58.60.208]:8333", "141.8.29.139:8333", "176.126.73.74:8333", "51.37.212.201:8333",
				"[::ffff:109.224.84.149]:8333", "[::ffff:123.202.192.214]:8333", "[::ffff:130.180.58.210]:8333",
				"[::ffff:144.2.65.179]:8333", "[::ffff:158.174.102.68]:8333", "[::ffff:158.248.16.134]:8333",
				"[::ffff:176.198.90.204]:8333", "[::ffff:176.9.150.253]:8333", "[::ffff:178.192.9.193]:8333",
				"[::ffff:178.26.46.97]:8333", "[::ffff:184.181.44.154]:8333", "[::ffff:217.92.137.136]:8333",
				"[::ffff:218.146.42.163]:8333", "[::ffff:24.134.6.165]:8333", "[::ffff:45.130.58.202]:8333",
				"[::ffff:45.41.51.43]:8333", "[::ffff:46.226.18.135]:8333", "[::ffff:46.229.238.187]:8333",
				"[::ffff:46.59.13.35]:8333", "[::ffff:47.181.150.194]:8333", "[::ffff:5.161.244.95]:8333",
				"[::ffff:62.177.111.78]:8333", "[::ffff:64.130.52.77]:8333", "[::ffff:71.185.186.173]:8333",
				"[::ffff:80.253.94.252]:8333", "[::ffff:82.67.127.46]:8333", "[::ffff:84.196.182.49]:8333",
				"[::ffff:86.242.20.49]:8333", "[::ffff:86.254.150.8]:8333", "[::ffff:87.236.195.198]:8333",
				"[::ffff:95.90.138.145]:8333", "179.51.86.34:8333", "188.63.47.92:8333",
				"54.248.26.73:8333", "82.67.161.60:8333", "92.252.69.140:8333", "92.98.3.189:8333",
				"[::ffff:109.202.209.123]:8333", "[::ffff:121.99.109.132]:8333", "[::ffff:13.48.242.195]:8333",
				"[::ffff:137.175.247.16]:8333", "[::ffff:141.195.182.138]:8333", "[::ffff:141.224.209.201]:8333",
				"[::ffff:15.134.155.244]:8333", "[::ffff:15.222.135.69]:8333", "[::ffff:159.196.59.9]:8333",
				"[::ffff:167.235.98.250]:8333", "[::ffff:174.165.47.165]:8333", "[::ffff:176.34.50.242]:8333",
				"[::ffff:178.26.219.209]:8333", "[::ffff:18.102.201.234]:8333", "[::ffff:181.115.88.2]:8333",
				"[::ffff:188.34.193.226]:8333", "[::ffff:194.160.169.63]:8333", "[::ffff:194.42.111.175]:8333",
				"[::ffff:203.12.2.113]:8333", "[::ffff:213.144.146.33]:8333", "[::ffff:3.23.202.194]:8333",
				"[::ffff:3.24.243.206]:8333", "[::ffff:38.172.231.13]:8333", "[::ffff:43.200.189.153]:8333",
				"[::ffff:43.202.209.174]:8333", "[::ffff:45.142.17.140]:8333", "[::ffff:47.130.216.204]:8333",
				"[::ffff:5.56.248.2]:8333", "[::ffff:50.106.24.94]:8333", "[::ffff:51.158.54.195]:8333",
				"[::ffff:51.44.200.138]:8333", "[::ffff:52.74.41.162]:8333", "[::ffff:54.246.85.218]:8333",
				"[::ffff:56.125.205.1]:8333", "[::ffff:56.125.249.13]:8333", "[::ffff:56.126.1.10]:8333",
				"[::ffff:56.126.33.251]:8333", "[::ffff:65.109.125.160]:8333", "[::ffff:68.103.11.30]:8333",
				"[::ffff:68.231.1.158]:8333", "[::ffff:71.179.175.122]:8333", "[::ffff:72.210.34.27]:8333",
				"[::ffff:72.253.193.231]:8333", "[::ffff:76.31.239.16]:8333", "[::ffff:77.162.78.194]:8333",
				"[::ffff:79.160.240.207]:8333", "[::ffff:82.66.107.156]:8333", "[::ffff:85.0.91.69]:8333",
				"[::ffff:85.3.52.172]:8333", "[::ffff:88.153.65.113]:8333", "[::ffff:89.217.193.252]:8333",
				"[::ffff:89.56.142.128]:8333", "[::ffff:89.56.206.21]:8333", "[::ffff:90.11.72.52]:8333",
				"[::ffff:90.189.215.153]:8333", "[::ffff:92.148.116.20]:8333", "[::ffff:93.56.5.69]:8333",
				"[::ffff:93.89.130.246]:8333"
			};

			using var insertCommand = connection.CreateCommand();
			insertCommand.CommandText = "INSERT INTO p2p_seed_node (node) VALUES (@node);";

			foreach (var node in initialNodes)
			{
				insertCommand.Parameters.Clear();
				insertCommand.Parameters.AddWithValue("@node", node);
				insertCommand.ExecuteNonQuery();
			}
		}
	}

	private static void CreateCompactBlockFilterTableIfMissing(SqliteConnection connection)
	{
		using var createCommand = connection.CreateCommand();

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
