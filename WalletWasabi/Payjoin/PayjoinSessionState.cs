namespace WalletWasabi.Payjoin;

public enum PayjoinSessionStatus
{
	/// <summary>Session is live, no original proposal from a sender yet.</summary>
	AwaitingSender,

	/// <summary>An original proposal arrived and is being validated/augmented.</summary>
	ProcessingProposal,

	/// <summary>The payjoin proposal was posted to the directory; watching for the transaction.</summary>
	ProposalSent,

	/// <summary>The payjoin transaction reached the wallet.</summary>
	Completed,

	/// <summary>The session expired before completing.</summary>
	Expired,

	/// <summary>The session ended without a payjoin (error reply sent, cancelled, or fallback).</summary>
	Failed,
}

/// <summary>Immutable UI-facing snapshot of a receiver session.</summary>
public record PayjoinSessionState(
	string SessionId,
	string WalletName,
	string Address,
	string? PjUri,
	PayjoinSessionStatus Status,
	string? ErrorMessage = null);

/// <summary>BIP 77 endpoints and receiver policy, resolved from the app configuration.</summary>
/// <param name="TorEnabled">
/// When Tor is on, every client from the HTTP factory rides Tor, so the OHTTP-keys
/// bootstrap fetches straight from the directory (it sees a Tor exit, the relay sees
/// nothing). When off, the bootstrap goes through a relay acting as a CONNECT proxy so
/// the directory never sees the client IP.
/// </param>
public record PayjoinConfiguration(
	string DirectoryUrl,
	string[] OhttpRelayUrls,
	ulong MaxFeeRateSatPerVb,
	bool TorEnabled);
