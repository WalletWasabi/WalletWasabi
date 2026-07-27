using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Data.Sqlite;

namespace WalletWasabi.Payjoin;

/// <summary>
/// SQLite-backed store for BIP 77 sender sessions: per-session metadata plus an
/// append-only event log consumed by payjoin-ffi's <c>JsonSenderSessionPersister</c>
/// contract. Every state-machine transition appends its event before the next state is
/// acted on (persist-before-act), so replaying the log after a crash resumes the exact
/// state. Sessions are deduplicated on the pj endpoint AND the receiver key — including
/// completed sessions — to prevent address/HPKE-key reuse (payjoin-cli parity).
/// </summary>
public class PayjoinSenderSessionStore : IDisposable
{
	private const string SessionColumns = "id, endpoint, receiver_key, wallet_name, fallback_tx, created_at, completed_at";

	private readonly SqliteConnection _connection;
	private readonly object _lock = new();
	private readonly ConcurrentDictionary<long, byte> _activeSessions = new();
	private bool _disposedValue;

	private PayjoinSenderSessionStore(SqliteConnection connection)
	{
		_connection = connection;
	}

	/// <param name="dataSource">Path to the SQLite database file, or <c>:memory:</c>.</param>
	public static PayjoinSenderSessionStore FromFile(string dataSource)
	{
		SqliteConnection? connectionToDispose = null;

		try
		{
			if (dataSource != ":memory:" && Path.GetDirectoryName(dataSource) is { Length: > 0 } dir)
			{
				Directory.CreateDirectory(dir);
			}

			SqliteConnectionStringBuilder builder = new() { DataSource = dataSource };
			SqliteConnection connection = new(builder.ConnectionString);
			connectionToDispose = connection;
			connection.Open();

			using (SqliteCommand command = connection.CreateCommand())
			{
				command.CommandText = """
					CREATE TABLE IF NOT EXISTS sender_session (
						id INTEGER PRIMARY KEY AUTOINCREMENT,
						endpoint TEXT NOT NULL,
						receiver_key TEXT NOT NULL,
						wallet_name TEXT NOT NULL,
						fallback_tx TEXT, /* hex of the finalized original tx; belt-and-suspenders for expiry recovery */
						created_at INTEGER NOT NULL, /* UNIX seconds */
						completed_at INTEGER /* NULL ~ session open */
					);
					CREATE UNIQUE INDEX IF NOT EXISTS sender_session_endpoint ON sender_session (endpoint);
					CREATE UNIQUE INDEX IF NOT EXISTS sender_session_receiver_key ON sender_session (receiver_key);
					CREATE TABLE IF NOT EXISTS sender_session_event (
						id INTEGER PRIMARY KEY AUTOINCREMENT,
						session_id INTEGER NOT NULL REFERENCES sender_session (id),
						event_json TEXT NOT NULL
					);
					CREATE INDEX IF NOT EXISTS sender_session_event_session ON sender_session_event (session_id);
					""";
				command.ExecuteNonQuery();
			}

			connectionToDispose = null;
			return new PayjoinSenderSessionStore(connection);
		}
		finally
		{
			connectionToDispose?.Close();
		}
	}

	/// <exception cref="PayjoinDuplicateSessionException">A session with the same endpoint or receiver key exists (open or completed).</exception>
	public PayjoinSenderSessionRecord CreateSession(string endpoint, string receiverKey, string walletName, string? fallbackTxHex = null)
	{
		lock (_lock)
		{
			if (TryFindNoLock(endpoint, receiverKey, out var existing))
			{
				throw new PayjoinDuplicateSessionException(existing);
			}

			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = """
				INSERT INTO sender_session (endpoint, receiver_key, wallet_name, fallback_tx, created_at)
				VALUES ($endpoint, $receiver_key, $wallet_name, $fallback_tx, unixepoch())
				RETURNING id, created_at;
				""";
			command.Parameters.AddWithValue("$endpoint", endpoint);
			command.Parameters.AddWithValue("$receiver_key", receiverKey);
			command.Parameters.AddWithValue("$wallet_name", walletName);
			command.Parameters.AddWithValue("$fallback_tx", (object?)fallbackTxHex ?? DBNull.Value);

			using SqliteDataReader reader = command.ExecuteReader();
			reader.Read();
			return new PayjoinSenderSessionRecord(
				Id: reader.GetInt64(0),
				Endpoint: endpoint,
				ReceiverKey: receiverKey,
				WalletName: walletName,
				FallbackTxHex: fallbackTxHex,
				CreatedAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)),
				IsCompleted: false);
		}
	}

	public bool TryFindSession(string endpoint, string receiverKey, [NotNullWhen(true)] out PayjoinSenderSessionRecord? record)
	{
		lock (_lock)
		{
			return TryFindNoLock(endpoint, receiverKey, out record);
		}
	}

	public void SetFallbackTx(long sessionId, string fallbackTxHex)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "UPDATE sender_session SET fallback_tx = $fallback_tx WHERE id = $id;";
			command.Parameters.AddWithValue("$fallback_tx", fallbackTxHex);
			command.Parameters.AddWithValue("$id", sessionId);
			command.ExecuteNonQuery();
		}
	}

	public void AppendEvent(long sessionId, string eventJson)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "INSERT INTO sender_session_event (session_id, event_json) VALUES ($session_id, $event_json);";
			command.Parameters.AddWithValue("$session_id", sessionId);
			command.Parameters.AddWithValue("$event_json", eventJson);
			command.ExecuteNonQuery();
		}
	}

	public IReadOnlyList<string> LoadEvents(long sessionId)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "SELECT event_json FROM sender_session_event WHERE session_id = $session_id ORDER BY id;";
			command.Parameters.AddWithValue("$session_id", sessionId);

			List<string> events = new();
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read())
			{
				events.Add(reader.GetString(0));
			}

			return events;
		}
	}

	/// <summary>Marks the session terminal. Idempotent.</summary>
	public void CompleteSession(long sessionId)
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = "UPDATE sender_session SET completed_at = unixepoch() WHERE id = $id AND completed_at IS NULL;";
			command.Parameters.AddWithValue("$id", sessionId);
			command.ExecuteNonQuery();
		}
	}

	public IReadOnlyList<PayjoinSenderSessionRecord> GetOpenSessions()
	{
		lock (_lock)
		{
			using SqliteCommand command = _connection.CreateCommand();
			command.CommandText = $"SELECT {SessionColumns} FROM sender_session WHERE completed_at IS NULL ORDER BY id;";

			List<PayjoinSenderSessionRecord> sessions = new();
			using SqliteDataReader reader = command.ExecuteReader();
			while (reader.Read())
			{
				sessions.Add(ReadRecord(reader));
			}

			return sessions;
		}
	}

	/// <summary>
	/// In-process guard: a session being driven by the send flow right now must not be
	/// touched by the background sweeper.
	/// </summary>
	public void MarkActive(long sessionId) => _activeSessions.TryAdd(sessionId, 0);

	public void UnmarkActive(long sessionId) => _activeSessions.TryRemove(sessionId, out _);

	public bool IsActive(long sessionId) => _activeSessions.ContainsKey(sessionId);

	private bool TryFindNoLock(string endpoint, string receiverKey, [NotNullWhen(true)] out PayjoinSenderSessionRecord? record)
	{
		using SqliteCommand command = _connection.CreateCommand();
		command.CommandText = $"SELECT {SessionColumns} FROM sender_session WHERE endpoint = $endpoint OR receiver_key = $receiver_key LIMIT 1;";
		command.Parameters.AddWithValue("$endpoint", endpoint);
		command.Parameters.AddWithValue("$receiver_key", receiverKey);

		using SqliteDataReader reader = command.ExecuteReader();
		if (reader.Read())
		{
			record = ReadRecord(reader);
			return true;
		}

		record = null;
		return false;
	}

	private static PayjoinSenderSessionRecord ReadRecord(SqliteDataReader reader) =>
		new(
			Id: reader.GetInt64(0),
			Endpoint: reader.GetString(1),
			ReceiverKey: reader.GetString(2),
			WalletName: reader.GetString(3),
			FallbackTxHex: reader.IsDBNull(4) ? null : reader.GetString(4),
			CreatedAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5)),
			IsCompleted: !reader.IsDBNull(6));

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
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}

public record PayjoinSenderSessionRecord(
	long Id,
	string Endpoint,
	string ReceiverKey,
	string WalletName,
	string? FallbackTxHex,
	DateTimeOffset CreatedAt,
	bool IsCompleted);

public class PayjoinDuplicateSessionException : WebClients.PayJoin.PayjoinException
{
	public PayjoinDuplicateSessionException(PayjoinSenderSessionRecord existing)
		: base(existing.IsCompleted
			? "A payjoin with this link was already completed. The payment can be sent as a normal transaction."
			: "A payjoin to this link is already in progress.")
	{
		Existing = existing;
	}

	public PayjoinSenderSessionRecord Existing { get; }
}
