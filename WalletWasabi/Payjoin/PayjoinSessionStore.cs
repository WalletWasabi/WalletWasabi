using Microsoft.Data.Sqlite;
using NBitcoin;
using System.Collections.Generic;
using System.Threading;

namespace WalletWasabi.Payjoin;

/// <summary>
/// Persistent storage for BIP 77 (async payjoin) receiver sessions.
///
/// Sessions are event-sourced: payjoin-ffi hands us opaque JSON events which are stored
/// in an append-only log and replayed on startup to reconstruct the typestate. Session
/// metadata rows carry what the log cannot: wallet association, the coin reserved as
/// receiver input contribution, and the expected payjoin txid for settlement detection.
/// The inputs-seen table is the probing-attack defense mandated by BIP 78/77: an outpoint
/// offered to us in an original proposal is never accepted twice, across restarts.
/// </summary>
public class PayjoinSessionStore : IDisposable
{
	private bool _disposedValue;

	/// <remarks>_connection cannot be accessed from multiple threads at the same time; all access is serialized by <see cref="_lock"/>.</remarks>
	private readonly SqliteConnection _connection;
	private readonly Lock _lock = new();

	private PayjoinSessionStore(SqliteConnection connection)
	{
		_connection = connection;
	}

	/// <param name="dataSource">Path to the SQLite database file, or special <c>:memory:</c> string.</param>
	/// <exception cref="SqliteException">If there is an issue with the operation.</exception>
	public static PayjoinSessionStore FromFile(string dataSource)
	{
		SqliteConnection? connectionToDispose = null;

		try
		{
			SqliteConnectionStringBuilder builder = new();
			builder.DataSource = dataSource;

			SqliteConnection connection = new(builder.ConnectionString);
			connectionToDispose = connection;
			connection.Open();

			using (SqliteCommand command = connection.CreateCommand())
			{
				command.CommandText = """
					CREATE TABLE IF NOT EXISTS receiver_session (
						id TEXT NOT NULL PRIMARY KEY,
						wallet_name TEXT NOT NULL,
						address TEXT NOT NULL,
						pj_uri TEXT, /* NULL until the session is initialized */
						reserved_outpoint BLOB, /* coin reserved as receiver input contribution; NULL if none yet */
						proposal_txid BLOB, /* expected payjoin txid (little endian); NULL until the proposal is posted */
						created_at INTEGER NOT NULL, /* UNIX timestamp in seconds */
						completed_at INTEGER /* NULL ~ session is active */
					);
					CREATE TABLE IF NOT EXISTS receiver_session_event (
						session_id TEXT NOT NULL,
						seq INTEGER NOT NULL,
						event_data TEXT NOT NULL, /* opaque JSON event from payjoin-ffi */
						PRIMARY KEY (session_id, seq)
					);
					CREATE TABLE IF NOT EXISTS input_seen (
						outpoint BLOB NOT NULL PRIMARY KEY,
						created_at INTEGER NOT NULL /* UNIX timestamp in seconds */
					);
					""";
				command.ExecuteNonQuery();
			}

			using (SqliteCommand walCommand = connection.CreateCommand())
			{
				walCommand.CommandText = """
					PRAGMA journal_mode = 'wal';
					PRAGMA synchronous  = 'NORMAL';
					""";
				walCommand.ExecuteNonQuery();
			}

			PayjoinSessionStore storage = new(connection);
			connectionToDispose = null;
			return storage;
		}
		finally
		{
			connectionToDispose?.Close();
			connectionToDispose?.Dispose();
		}
	}

	/// <summary>Creates a new active session row and returns its identifier.</summary>
	public string CreateSession(string walletName, string address)
	{
		string id = Guid.NewGuid().ToString("N");

		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = """
				INSERT INTO receiver_session (id, wallet_name, address, created_at)
				VALUES ($id, $wallet_name, $address, $created_at);
				""";
			command.Parameters.AddWithValue("$id", id);
			command.Parameters.AddWithValue("$wallet_name", walletName);
			command.Parameters.AddWithValue("$address", address);
			command.Parameters.AddWithValue("$created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
			command.ExecuteNonQuery();
		}

		return id;
	}

	/// <summary>Appends one payjoin-ffi event to the session's log.</summary>
	public void AppendEvent(string sessionId, string eventData)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = """
				INSERT INTO receiver_session_event (session_id, seq, event_data)
				VALUES ($session_id, (SELECT IFNULL(MAX(seq), 0) + 1 FROM receiver_session_event WHERE session_id = $session_id), $event_data);
				""";
			command.Parameters.AddWithValue("$session_id", sessionId);
			command.Parameters.AddWithValue("$event_data", eventData);
			command.ExecuteNonQuery();
		}
	}

	/// <summary>Loads the session's event log in append order.</summary>
	public string[] LoadEvents(string sessionId)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "SELECT event_data FROM receiver_session_event WHERE session_id = $session_id ORDER BY seq;";
			command.Parameters.AddWithValue("$session_id", sessionId);

			List<string> events = new();
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read())
			{
				events.Add(reader.GetString(0));
			}

			return events.ToArray();
		}
	}

	/// <summary>Marks the session as no longer active. Idempotent.</summary>
	public void CloseSession(string sessionId)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "UPDATE receiver_session SET completed_at = $completed_at WHERE id = $id AND completed_at IS NULL;";
			command.Parameters.AddWithValue("$completed_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
			command.Parameters.AddWithValue("$id", sessionId);
			command.ExecuteNonQuery();
		}
	}

	public void SetPjUri(string sessionId, string pjUri)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "UPDATE receiver_session SET pj_uri = $pj_uri WHERE id = $id;";
			command.Parameters.AddWithValue("$pj_uri", pjUri);
			command.Parameters.AddWithValue("$id", sessionId);
			command.ExecuteNonQuery();
		}
	}

	public void SetReservedOutpoint(string sessionId, OutPoint outpoint)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "UPDATE receiver_session SET reserved_outpoint = $outpoint WHERE id = $id;";
			command.Parameters.AddWithValue("$outpoint", outpoint.ToBytes());
			command.Parameters.AddWithValue("$id", sessionId);
			command.ExecuteNonQuery();
		}
	}

	public void SetProposalTxid(string sessionId, uint256 txid)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "UPDATE receiver_session SET proposal_txid = $txid WHERE id = $id;";
			command.Parameters.AddWithValue("$txid", txid.ToBytes());
			command.Parameters.AddWithValue("$id", sessionId);
			command.ExecuteNonQuery();
		}
	}

	public IReadOnlyList<PayjoinSessionRecord> GetActiveSessions()
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = $"SELECT {SessionColumns} FROM receiver_session WHERE completed_at IS NULL ORDER BY created_at;";

			List<PayjoinSessionRecord> sessions = new();
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read())
			{
				sessions.Add(ReadSession(reader));
			}

			return sessions;
		}
	}

	public PayjoinSessionRecord? TryGetSession(string sessionId)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = $"SELECT {SessionColumns} FROM receiver_session WHERE id = $id;";
			command.Parameters.AddWithValue("$id", sessionId);

			using SqliteDataReader reader = command.ExecuteReader();
			return reader.Read() ? ReadSession(reader) : null;
		}
	}

	/// <summary>
	/// Records that an outpoint was offered to us as a sender input.
	/// </summary>
	/// <returns><c>true</c> if the outpoint was newly recorded, <c>false</c> if it was seen before.</returns>
	public bool TryInsertInputSeen(OutPoint outpoint)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "INSERT OR IGNORE INTO input_seen (outpoint, created_at) VALUES ($outpoint, $created_at);";
			command.Parameters.AddWithValue("$outpoint", outpoint.ToBytes());
			command.Parameters.AddWithValue("$created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
			return command.ExecuteNonQuery() > 0;
		}
	}

	private const string SessionColumns = "id, wallet_name, address, pj_uri, reserved_outpoint, proposal_txid, created_at, completed_at";

	private static PayjoinSessionRecord ReadSession(SqliteDataReader reader)
	{
		return new PayjoinSessionRecord(
			Id: reader.GetString(0),
			WalletName: reader.GetString(1),
			Address: reader.GetString(2),
			PjUri: reader.IsDBNull(3) ? null : reader.GetString(3),
			ReservedOutpoint: reader.IsDBNull(4) ? null : OutPointFromBytes((byte[])reader.GetValue(4)),
			ProposalTxid: reader.IsDBNull(5) ? null : new uint256((byte[])reader.GetValue(5)),
			CreatedAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)),
			CompletedAt: reader.IsDBNull(7) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(7)));
	}

	private static OutPoint OutPointFromBytes(byte[] bytes)
	{
		OutPoint outpoint = new();
		outpoint.FromBytes(bytes);
		return outpoint;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_connection.Close();
				_connection.Dispose();
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}

public record PayjoinSessionRecord(
	string Id,
	string WalletName,
	string Address,
	string? PjUri,
	OutPoint? ReservedOutpoint,
	uint256? ProposalTxid,
	DateTimeOffset CreatedAt,
	DateTimeOffset? CompletedAt);
