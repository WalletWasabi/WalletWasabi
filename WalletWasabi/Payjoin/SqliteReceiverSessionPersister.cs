using Payjoin;

namespace WalletWasabi.Payjoin;

/// <summary>
/// payjoin-ffi persister bound to one session's append-only event log in
/// <see cref="PayjoinSessionStore"/>. The ffi calls <see cref="Save"/> as part of every
/// typestate transition (persist-before-act), <see cref="Load"/> on replay, and
/// <see cref="Close"/> when the session reaches a terminal state.
/// </summary>
public class SqliteReceiverSessionPersister : JsonReceiverSessionPersister
{
	private readonly PayjoinSessionStore _store;
	private readonly string _sessionId;

	public SqliteReceiverSessionPersister(PayjoinSessionStore store, string sessionId)
	{
		_store = store;
		_sessionId = sessionId;
	}

	public void Save(string @event) => _store.AppendEvent(_sessionId, @event);

	public string[] Load() => _store.LoadEvents(_sessionId);

	public void Close() => _store.CloseSession(_sessionId);
}
