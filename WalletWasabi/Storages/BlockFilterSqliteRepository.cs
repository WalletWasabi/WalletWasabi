using Microsoft.Data.Sqlite;
using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Storages;

/// <summary>
/// SQLite-based repository for compact block filters.
/// </summary>
/// <remarks>The implementation is thread-safe because <see cref="SqliteConnection"/> are created per operation. The connection pool is used for efficiency.</remarks>
public class BlockFilterSqliteRepository
{
	private readonly SqliteConnectionFactory _connectionFactory;

	public BlockFilterSqliteRepository(SqliteConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	/// <summary>
	/// Creates and opens a new pooled connection.
	/// </summary>
	public SqliteConnection CreateConnection()
		=> _connectionFactory();

	/// <summary>
	/// Begin a transaction.
	/// </summary>
	/// <seealso href="https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions"/>
	public SqliteTransaction BeginTransaction(SqliteConnection connection)
	{
		return connection.BeginTransaction();
	}

	/// <summary>
	/// Returns all filters with height ≥ than the given one.
	/// </summary>
	/// <param name="limit">If a maximum number is specified, the number of returned records is limited to this value.</param>
	public IEnumerable<FilterModel> Fetch(uint fromHeight, int limit = -1)
	{
		using var connection = _connectionFactory();
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
		using var connection = _connectionFactory();
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
		using var connection = _connectionFactory();
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
		using var connection = _connectionFactory();
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
		using var connection = _connectionFactory();
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
			using var connection = _connectionFactory();
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
		using var connection = _connectionFactory();
		using var command = connection.CreateCommand();
		command.CommandText = $"PRAGMA user_version = {newUserVersion};";
		command.ExecuteNonQuery();
	}

	public int GetPragmaUserVersion()
	{
		using var connection = _connectionFactory();
		using var command = connection.CreateCommand();
		command.CommandText = "PRAGMA user_version";
		var tmp = Convert.ToInt32(command.ExecuteScalar());
		return tmp;
	}

	public uint? GetMinimumBlockHeight()
	{
		using var connection = _connectionFactory();
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
		using var connection = _connectionFactory();
		using var createCommand = connection.CreateCommand();
		createCommand.CommandText = "DELETE FROM filter";
		int affectedLines = createCommand.ExecuteNonQuery();

		return affectedLines > 0;
	}
}
