using Microsoft.Data.Sqlite;
using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Stores;

/// <summary>
/// SQLite-based storage for block filters.
/// </summary>
/// <remarks>The implementation is thread-safe because <see cref="SqliteConnection"/> are created per operation. The connection pool is used for efficiency.</remarks>
/// <seealso href="https://learn.microsoft.com/en-gb/dotnet/standard/data/sqlite/async">
/// Async is not supported at the moment, so no async methods were used in this implementation.
/// </seealso>
public class BlockFilterSqliteStorage : IDisposable
{
	private readonly string _connectionString;
	private bool _disposedValue;

	private BlockFilterSqliteStorage(string connectionString)
	{
		_connectionString = connectionString;
	}

	/// <summary>
	/// Opens a new SQLite connection to the given database file.
	/// </summary>
	/// <param name="filePath">Path to the SQLite database file.</param>
	/// <param name="startingFilter">Starting filter to put into the filter table if the table needs to be created.</param>
	/// <exception cref="InvalidOperationException">If there is an unrecoverable error.</exception>
	/// <seealso href="https://dev.to/lefebvre/speed-up-sqlite-with-write-ahead-logging-wal-do">Write-ahead logging explained.</seealso>
	public static BlockFilterSqliteStorage FromFile(string filePath)
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
	public SqliteConnection CreateConnection()
	{
		var connection = new SqliteConnection(_connectionString);
		connection.Open();
		return connection;
	}

	/// <summary>
	/// Returns all filters with height ≥ than the given one.
	/// </summary>
	/// <param name="limit">If a maximum number is specified, the number of returned records is limited to this value.</param>
	public IEnumerable<FilterModel> Fetch(uint fromHeight, int limit = -1)
	{
		using var connection = CreateConnection();
		using var command = connection.CreateCommand();

		command.CommandText = "SELECT * FROM filter WHERE block_height >= $block_height ORDER BY block_height LIMIT $limit";
		command.Parameters.AddWithValue("$block_height", fromHeight);
		command.Parameters.AddWithValue("$limit", limit);

		using var reader = command.ExecuteReader();

		while (reader.Read())
		{
			FilterModel filter = ReadRow(reader);
			yield return filter;
		}
	}

	/// <summary>
	/// Returns last <paramref name="n"/> filters from the database table.
	/// </summary>
	public IEnumerable<FilterModel> FetchLast(int n)
	{
		using var connection = CreateConnection();
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT * FROM filter WHERE block_height > (SELECT MAX(block_height) - $n FROM filter) ORDER BY block_height";
		command.Parameters.AddWithValue("$n", n);

		using var reader = command.ExecuteReader();

		while (reader.Read())
		{
			FilterModel filter = ReadRow(reader);
			yield return filter;
		}
	}

	/// <summary>
	/// Removes the filter with the highest height from the database table.
	/// </summary>
	public bool TryRemoveLast([NotNullWhen(true)] out FilterModel? filter)
	{
		using var connection = CreateConnection();
		using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM filter WHERE block_height = (SELECT MAX(block_height) FROM filter) returning *";

		using var reader = command.ExecuteReader();

		if (!reader.Read())
		{
			filter = null;
			return false;
		}

		// Stored filters are considered to be valid.
		filter = ReadRow(reader);
		return true;
	}

	/// <summary>
	/// Removes the filter with the highest height from the table if the last filter has higher height than <paramref name="height"/>.
	/// </summary>
	/// <param name="height">Minimum block height of the last block to remove (exclusive).</param>
	public bool TryRemoveLastIfNewerThan(uint height, [NotNullWhen(true)] out FilterModel? filter)
	{
		using var connection = CreateConnection();
		using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM filter WHERE block_height > $block_height AND block_height = (SELECT MAX(block_height) FROM filter) returning *";
		command.Parameters.AddWithValue("$block_height", height);

		using var reader = command.ExecuteReader();

		if (!reader.Read())
		{
			filter = null;
			return false;
		}

		// Stored filters are considered to be valid.
		filter = ReadRow(reader);
		return true;
	}

	/// <summary>
	/// Removes the filters with higher height than <paramref name="height"/>.
	/// </summary>
	/// <param name="height">Minimum block height of the last block to remove (exclusive).</param>
	public IEnumerable<FilterModel> RemoveNewerThan(uint height)
	{
		using var connection = CreateConnection();
		using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM filter WHERE block_height > $block_height RETURNING *";
		command.Parameters.AddWithValue("$block_height", height);

		using var reader = command.ExecuteReader();

		List<FilterModel> removedFilters = [];

		while (reader.Read())
		{
			removedFilters.Add(ReadRow(reader));
		}

		return removedFilters;
	}

	private FilterModel ReadRow(SqliteDataReader reader)
	{
		uint blockHeight = (uint)reader.GetInt64(ordinal: 0);
		uint256 blockHash = new(reader.GetFieldValue<byte[]>(ordinal: 1));
		byte[] filterData = reader.GetFieldValue<byte[]>(ordinal: 2);
		uint256 prevBlockHash = new(reader.GetFieldValue<byte[]>(ordinal: 3));
		long blockTime = reader.GetInt64(ordinal: 4);

		return FilterModel.Create(blockHeight, blockHash, filterData, prevBlockHash, blockTime);
	}

	/// <summary>
	/// Append a new filter to the table.
	/// </summary>
	/// <returns><c>true</c> if a row was appended, <c>false</c> otherwise (even in case of an error).</returns>
	public bool TryAppend(FilterModel filter)
	{
		try
		{
			using var connection = CreateConnection();
			return TryAppend(connection, filter);
		}
		catch (SqliteException)
		{
			return false;
		}
	}

	public bool TryAppend(SqliteConnection connection, FilterModel filter)
	{
		try
		{
			using var insertCommand = connection.CreateCommand();
			insertCommand.CommandText = """
				INSERT INTO filter (block_height, block_hash, filter_data, previous_block_hash, epoch_block_time)
				VALUES ($block_height, $block_hash, $filter_data, $previous_block_hash, $epoch_block_time)
				""";
			insertCommand.Parameters.AddWithValue("$block_height", filter.Header.Height.Height);
			insertCommand.Parameters.AddWithValue("$block_hash", filter.Header.BlockHash.ToBytes(lendian: true));
			insertCommand.Parameters.AddWithValue("$filter_data", filter.FilterData);
			insertCommand.Parameters.AddWithValue("$previous_block_hash", filter.Header.BlockFilterHeader.ToBytes(lendian: true));
			insertCommand.Parameters.AddWithValue("$epoch_block_time", filter.Header.EpochBlockTime);
			int result = insertCommand.ExecuteNonQuery();

			return result > 0;
		}
		catch (SqliteException)
		{
			return false;
		}
	}

	public void SetPragmaUserVersion(int newUserVersion)
	{
		using var connection = CreateConnection();
		using var command = connection.CreateCommand();
		command.CommandText = $"PRAGMA user_version = {newUserVersion};";
		command.ExecuteNonQuery();
	}

	public int GetPragmaUserVersion()
	{
		using var connection = CreateConnection();
		using var command = connection.CreateCommand();
		command.CommandText = "PRAGMA user_version";
		var tmp = Convert.ToInt32(command.ExecuteScalar());
		return tmp;
	}

	public uint? GetMinimumBlockHeight()
	{
		using var connection = CreateConnection();
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT MIN(block_height) FROM filter";

		var result = command.ExecuteScalar();
		return result is not null && result != DBNull.Value ? Convert.ToUInt32(result) : null;
	}

	/// <summary>
	/// Clears the filter table.
	/// </summary>
	/// <returns><c>true</c> if at least one row was deleted, <c>false</c> otherwise.</returns>
	public bool Clear()
	{
		using var connection = CreateConnection();
		using var createCommand = connection.CreateCommand();
		createCommand.CommandText = "DELETE FROM filter";
		int affectedLines = createCommand.ExecuteNonQuery();

		return affectedLines > 0;
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
