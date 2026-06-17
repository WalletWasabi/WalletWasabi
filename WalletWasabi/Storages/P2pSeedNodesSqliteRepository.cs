using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace WalletWasabi.Storages;

/// <summary>
/// SQLite-based repository for P2P node seed data.
/// </summary>
/// <remarks>The implementation is thread-safe because <see cref="SqliteConnection"/> are created per operation. The connection pool is used for efficiency.</remarks>
public class P2pSeedNodesSqliteRepository
{
	private readonly SqliteConnectionFactory _connectionFactory;

	public P2pSeedNodesSqliteRepository(SqliteConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	/// <summary>
	/// Returns all nodes.
	/// </summary>
	public IEnumerable<string> FetchAll()
	{
		using var connection = _connectionFactory();
		using var command = connection.CreateCommand();

		command.CommandText = "SELECT * FROM p2p_seed_node";

		using var reader = command.ExecuteReader();

		while (reader.Read())
		{
			string node = ReadRow(reader);
			yield return node;
		}
	}

	private string ReadRow(SqliteDataReader reader) =>
		reader.GetString(ordinal: 0);

	/// <summary>
	/// Append a new node to the table.
	/// </summary>
	/// <returns><c>true</c> if a row was appended, <c>false</c> otherwise (even in case of an error).</returns>
	public bool TryAppend(string node)
	{
		try
		{
			using var connection = _connectionFactory();
			using var insertCommand = connection.CreateCommand();
			insertCommand.CommandText = """
				INSERT INTO p2p_seed_node (node)
				VALUES ($node)
				""";
			insertCommand.Parameters.AddWithValue("$node", node);
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
		createCommand.CommandText = "DELETE FROM p2p_seed_node";
		int affectedLines = createCommand.ExecuteNonQuery();

		return affectedLines > 0;
	}
}
