namespace WalletWasabi.BitcoinP2p;

/// <summary>
/// The components that are downloaded from the Bitcoin P2P network, in the order they are downloaded.
/// </summary>
/// <remarks>
/// A component is requested only once the ones before it are complete. That way a peer is asked for one kind of
/// data at a time, the bandwidth goes to the component that the rest is waiting for, and a peer that fails to
/// deliver can be held accountable for the one thing it was asked for.
/// </remarks>
public enum SyncStage
{
	/// <summary>The block header chain is catching up with the peers. Every peer can serve block headers.</summary>
	BlockHeaders,

	/// <summary>The compact filter headers of the known blocks are being downloaded.</summary>
	FilterHeaders,

	/// <summary>The compact filters of the known filter headers are being downloaded.</summary>
	CompactFilters,

	/// <summary>Everything else is downloaded. The blocks that the wallets need are the only thing left.</summary>
	Blocks
}

public static class SyncStageExtensions
{
	/// <summary>
	/// Tells whether a block can be requested from a peer.
	/// </summary>
	/// <remarks>
	/// Blocks come last, with one exception: while the filter headers and the compact filters are downloaded,
	/// a peer that does not serve them has nothing else to do, so it can serve blocks already.
	/// </remarks>
	public static bool AllowsBlockDownloadFrom(this SyncStage stage, bool peerServesCompactFilters) =>
		stage switch
		{
			SyncStage.BlockHeaders => false,
			SyncStage.FilterHeaders or SyncStage.CompactFilters => !peerServesCompactFilters,
			_ => true
		};
}
