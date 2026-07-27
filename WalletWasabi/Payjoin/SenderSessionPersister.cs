using System.Linq;
using Payjoin;

namespace WalletWasabi.Payjoin;

/// <summary>
/// Adapts one store session's append-only event log to payjoin-ffi's
/// <see cref="JsonSenderSessionPersister"/> callback contract. The ffi calls
/// <see cref="Save"/> as part of every state transition (persist-before-act) and
/// <see cref="Close"/> when the session reaches a terminal state.
/// </summary>
internal class SenderSessionPersister : JsonSenderSessionPersister
{
	private readonly PayjoinSenderSessionStore _store;
	private readonly long _sessionId;

	public SenderSessionPersister(PayjoinSenderSessionStore store, long sessionId)
	{
		_store = store;
		_sessionId = sessionId;
	}

	public void Save(string @event) => _store.AppendEvent(_sessionId, @event);

	public string[] Load() => _store.LoadEvents(_sessionId).ToArray();

	public void Close() => _store.CompleteSession(_sessionId);
}
