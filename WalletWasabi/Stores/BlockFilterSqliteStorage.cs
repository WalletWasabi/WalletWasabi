using Microsoft.Data.Sqlite;
using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Stores;

/// <summary>
/// SQLite-based storage for block filters.
/// </summary>
/// <remarks>The implementation is not thread-safe because <see cref="SqliteConnection"/> is not thread-safe.</remarks>
/// <seealso href="https://learn.microsoft.com/en-gb/dotnet/standard/data/sqlite/async">
/// Async is not supported at the moment, so no async methods were used in this implementation.
/// </seealso>
public class BlockFilterSqliteStorage : IDisposable
{
	private bool _disposedValue;

	private BlockFilterSqliteStorage(SqliteConnection connection)
	{
		Connection = connection;
	}

	/// <remarks>Connection cannot be accessed from multiple threads at the same time.</remarks>
	private SqliteConnection Connection { get; }

	/// <summary>
	/// Opens a new SQLite connection to the given database file.
	/// </summary>
	/// <param name="dataSource">Path to the SQLite database file, or special <c>:memory:</c> string.</param>
	/// <param name="startingFilter">Starting filter to put into the filter table if the table needs to be created.</param>
	/// <exception cref="InvalidOperationException">If there is an unrecoverable error.</exception>
	/// <seealso href="https://dev.to/lefebvre/speed-up-sqlite-with-write-ahead-logging-wal-do">Write-ahead logging explained.</seealso>
	public static BlockFilterSqliteStorage FromFile(string dataSource, FilterModel? startingFilter = null)
	{
		// In case there is an exception, we need to dispose things properly.
		SqliteConnection? connectionToDispose = null;
		BlockFilterSqliteStorage? storageToDispose = null;

		try
		{
			SqliteConnectionStringBuilder builder = new();
			builder.DataSource = dataSource;

			SqliteConnection connection = new(builder.ConnectionString);
			connectionToDispose = connection;
			connection.Open();

			using (SqliteCommand createCommand = connection.CreateCommand())
			{
				createCommand.CommandText = """
					CREATE TABLE IF NOT EXISTS filter (
						block_height INTEGER NOT NULL PRIMARY KEY,
						block_hash BLOB NOT NULL,
						filter_data BLOB NOT NULL,
						previous_block_hash BLOB NOT NULL,
						epoch_block_time INTEGER NOT NULL
					)
					""";
				createCommand.ExecuteNonQuery();
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

			BlockFilterSqliteStorage storage = new(connection);
			storageToDispose = storage;
			connectionToDispose = null;

			if (startingFilter is not null)
			{
				using SqliteCommand isEmptyCommand = connection.CreateCommand();
				isEmptyCommand.CommandText = "SELECT EXISTS (SELECT 1 FROM filter)";
				int existRows = Convert.ToInt32(isEmptyCommand.ExecuteScalar());

				if (existRows == 0)
				{
					if (!storage.TryAppend(startingFilter))
					{
						// Unrecoverable error.
						throw new InvalidOperationException("Failed to add the first filter to the database.");
					}
				}
			}

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
	/// Begin a transaction.
	/// </summary>
	/// <seealso href="https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions"/>
	public SqliteTransaction BeginTransaction()
	{
		return Connection.BeginTransaction();
	}

	/// <summary>
	/// Returns all filters with height ≥ than the given one.
	/// </summary>
	/// <param name="limit">If a maximum number is specified, the number of returned records is limited to this value.</param>
	public IEnumerable<FilterModel> Fetch(uint fromHeight, int limit = -1)
	{
		using SqliteCommand command = Connection.CreateCommand();

		command.CommandText = "SELECT * FROM filter WHERE block_height >= $block_height ORDER BY block_height LIMIT $limit";
		command.Parameters.AddWithValue("$block_height", fromHeight);
		command.Parameters.AddWithValue("$limit", limit);

		using SqliteDataReader reader = command.ExecuteReader();

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
		using SqliteCommand command = Connection.CreateCommand();
		command.CommandText = "SELECT * FROM filter WHERE block_height > (SELECT MAX(block_height) - $n FROM filter) ORDER BY block_height";
		command.Parameters.AddWithValue("$n", n);

		using SqliteDataReader reader = command.ExecuteReader();

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
		using SqliteCommand command = Connection.CreateCommand();
		command.CommandText = "DELETE FROM filter WHERE block_height = (SELECT MAX(block_height) FROM filter) returning *";

		using SqliteDataReader reader = command.ExecuteReader();

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
		using SqliteCommand command = Connection.CreateCommand();
		command.CommandText = "DELETE FROM filter WHERE block_height > $block_height AND block_height = (SELECT MAX(block_height) FROM filter) returning *";
		command.Parameters.AddWithValue("$block_height", height);

		using SqliteDataReader reader = command.ExecuteReader();

		if (!reader.Read())
		{
			filter = null;
			return false;
		}

		// Stored filters are considered to be valid.
		filter = ReadRow(reader);
		return true;
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
			using SqliteCommand insertCommand = Connection.CreateCommand();
			insertCommand.CommandText = """
				INSERT INTO filter (block_height, block_hash, filter_data, previous_block_hash, epoch_block_time)
				VALUES ($block_height, $block_hash, $filter_data, $previous_block_hash, $epoch_block_time)
				""";
			insertCommand.Parameters.AddWithValue("$block_height", filter.Header.Height);
			insertCommand.Parameters.AddWithValue("$block_hash", filter.Header.BlockHash.ToBytes(lendian: true));
			insertCommand.Parameters.AddWithValue("$filter_data", filter.FilterData);
			insertCommand.Parameters.AddWithValue("$previous_block_hash", filter.Header.PrevHash.ToBytes(lendian: true));
			insertCommand.Parameters.AddWithValue("$epoch_block_time", filter.Header.EpochBlockTime);
			int result = insertCommand.ExecuteNonQuery();

			return result > 0;
		}
		catch (SqliteException)
		{
			return false;
		}
	}

	/// <summary>
	/// Append filters in bulk to the table.
	/// </summary>
	/// <exception cref="SqliteException">If there is an issue with adding a new record.</exception>
	public void BulkAppend(IReadOnlyList<FilterModel> filters)
	{
		using SqliteTransaction transaction = Connection.BeginTransaction();

		using SqliteCommand command = Connection.CreateCommand();
		command.CommandText = """
			INSERT INTO filter (block_height, block_hash, filter_data, previous_block_hash, epoch_block_time)
			VALUES ($block_height, $block_hash, $filter_data, $previous_block_hash, $epoch_block_time)
			""";

		SqliteParameter blockHeightParameter = command.CreateParameter();
		blockHeightParameter.ParameterName = "$block_height";
		command.Parameters.Add(blockHeightParameter);

		SqliteParameter blockHashParameter = command.CreateParameter();
		blockHashParameter.ParameterName = "$block_hash";
		command.Parameters.Add(blockHashParameter);

		SqliteParameter filterDataParameter = command.CreateParameter();
		filterDataParameter.ParameterName = "$filter_data";
		command.Parameters.Add(filterDataParameter);

		SqliteParameter prevBlockHashParameter = command.CreateParameter();
		prevBlockHashParameter.ParameterName = "$previous_block_hash";
		command.Parameters.Add(prevBlockHashParameter);

		SqliteParameter epochBlockTimeParameter = command.CreateParameter();
		epochBlockTimeParameter.ParameterName = "$epoch_block_time";
		command.Parameters.Add(epochBlockTimeParameter);

		foreach (FilterModel filter in filters)
		{
			blockHeightParameter.Value = filter.Header.Height;
			blockHashParameter.Value = filter.Header.BlockHash.ToBytes(lendian: true);
			filterDataParameter.Value = filter.FilterData;
			prevBlockHashParameter.Value = filter.Header.PrevHash.ToBytes(lendian: true);
			epochBlockTimeParameter.Value = filter.Header.EpochBlockTime;

			_ = command.ExecuteNonQuery();
		}

		transaction.Commit();
	}

	/// <summary>
	/// Append filters in bulk to the table.
	/// </summary>
	/// <param name="filters">Raw filter lines from old mature index file.</param>
	/// <remarks>The method is meant for migration purposes.</remarks>
	/// <exception cref="SqliteException">If there is an issue with adding a new record.</exception>
	public void BulkAppend(IReadOnlyList<string> filters)
	{
		using SqliteTransaction transaction = Connection.BeginTransaction();

		using SqliteCommand command = Connection.CreateCommand();
		command.CommandText = """
			INSERT INTO filter (block_height, block_hash, filter_data, previous_block_hash, epoch_block_time)
			VALUES ($block_height, $block_hash, $filter_data, $previous_block_hash, $epoch_block_time)
			""";

		SqliteParameter blockHeightParameter = command.CreateParameter();
		blockHeightParameter.ParameterName = "$block_height";
		command.Parameters.Add(blockHeightParameter);

		SqliteParameter blockHashParameter = command.CreateParameter();
		blockHashParameter.ParameterName = "$block_hash";
		command.Parameters.Add(blockHashParameter);

		SqliteParameter filterDataParameter = command.CreateParameter();
		filterDataParameter.ParameterName = "$filter_data";
		command.Parameters.Add(filterDataParameter);

		SqliteParameter prevBlockHashParameter = command.CreateParameter();
		prevBlockHashParameter.ParameterName = "$previous_block_hash";
		command.Parameters.Add(prevBlockHashParameter);

		SqliteParameter epochBlockTimeParameter = command.CreateParameter();
		epochBlockTimeParameter.ParameterName = "$epoch_block_time";
		command.Parameters.Add(epochBlockTimeParameter);

		foreach (string line in filters)
		{
			ReadOnlySpan<char> span = line;

			int m1 = line.IndexOf(':', 0);
			int m2 = line.IndexOf(':', m1 + 1);
			int m3 = line.IndexOf(':', m2 + 1);
			int m4 = line.IndexOf(':', m3 + 1);

			if (m1 == -1 || m2 == -1 || m3 == -1 || m4 == -1)
			{
				throw new ArgumentException(line, nameof(line));
			}

			uint blockHeight = uint.Parse(span[..m1]);
			byte[] blockHash = Convert.FromHexString(span[(m1 + 1)..m2]);
			Array.Reverse(blockHash);
			byte[] filterData = Convert.FromHexString(span[(m2 + 1)..m3]);
			byte[] prevBlockHash = Convert.FromHexString(span[(m3 + 1)..m4]);
			Array.Reverse(prevBlockHash);
			long blockTime = long.Parse(span[(m4 + 1)..]);

			blockHeightParameter.Value = blockHeight;
			blockHashParameter.Value = blockHash;
			filterDataParameter.Value = filterData;
			prevBlockHashParameter.Value = prevBlockHash;
			epochBlockTimeParameter.Value = blockTime;

			_ = command.ExecuteNonQuery();
		}

		transaction.Commit();
	}

	/// <summary>
	/// Clears the filter table.
	/// </summary>
	/// <returns><c>true</c> if at least one row was deleted, <c>false</c> otherwise.</returns>
	public bool Clear()
	{
		using SqliteCommand createCommand = Connection.CreateCommand();
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
