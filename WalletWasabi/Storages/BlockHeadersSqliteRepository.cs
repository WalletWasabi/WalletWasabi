using Microsoft.Data.Sqlite;
using NBitcoin;
using System.Collections.Generic;
using System.Data;

namespace WalletWasabi.Storages;

/// <summary>
/// SQLite-based repository for Bitcoin block header chain.
/// </summary>
/// <remarks>The implementation is thread-safe because <see cref="SqliteConnection"/> are created per operation. The connection pool is used for efficiency.</remarks>
public class BlockHeadersSqliteRepository
{
	private readonly SqliteConnectionFactory _connectionFactory;

	public BlockHeadersSqliteRepository(SqliteConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	/// <summary>
	/// Returns all block headers serialized using NBitcoin's <see cref="BitcoinSerializableExtensions.ToBytes(IBitcoinSerializable, uint?)"/>.
	/// </summary>
	public IEnumerable<byte[]> FetchAll()
	{
		using var connection = _connectionFactory();
		using var command = connection.CreateCommand();

		command.CommandText = "SELECT header_bytes FROM block_header";

		using var reader = command.ExecuteReader();

		while (reader.Read())
		{
			var node = ReadRow(reader);
			yield return node;
		}
	}

	private byte[] ReadRow(SqliteDataReader reader) =>
		reader.GetFieldValue<byte[]>(ordinal: 0);

	/// <summary>
	/// Append a new block header to the table.
	/// </summary>
	/// <returns><c>true</c> if a row was appended, <c>false</c> otherwise (even in case of an error).</returns>
	public bool TryAppend(int height, BlockHeader header)
	{
		try
		{
			using var connection = _connectionFactory();
			using var insertCommand = connection.CreateCommand();
			insertCommand.CommandText = """
				INSERT INTO block_header (height, block_hash, header_bytes)
				VALUES ($height, $block_hash, $header_bytes)
				""";
			insertCommand.Parameters.AddWithValue("$height", height);
			insertCommand.Parameters.AddWithValue("$block_hash", header.GetHash().ToBytes(lendian: true));
			insertCommand.Parameters.AddWithValue("$header_bytes", header.ToBytes());
			int result = insertCommand.ExecuteNonQuery();

			return result > 0;
		}
		catch (SqliteException)
		{
			return false;
		}
	}

	/// <summary>
	/// Clears the table.
	/// </summary>
	/// <returns><c>true</c> if at least one row was deleted, <c>false</c> otherwise.</returns>
	public bool Clear()
	{
		using var connection = _connectionFactory();
		using var createCommand = connection.CreateCommand();
		createCommand.CommandText = "DELETE FROM block_header";
		int affectedLines = createCommand.ExecuteNonQuery();

		return affectedLines > 0;
	}
}
