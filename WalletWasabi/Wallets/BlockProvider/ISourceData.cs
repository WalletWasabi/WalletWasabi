using NBitcoin.Protocol;

namespace WalletWasabi.Wallets.BlockProvider;

/// <summary>
/// Source that provided a bitcoin block to us.
/// </summary>
public enum Source
{
	/// <summary>Wasabi Wallet file-system cache.</summary>
	/// <remarks>The assumption is that the cache is tested always before real block sources.</remarks>
	FileSystemCache = 0,

	/// <summary>Trusted full node that can provide blocks and there is no concern over privacy.</summary>
	TrustedFullNode = 1,

	/// <summary>Node from the P2P Network that provided a block to us.</summary>
	P2P = 2,
}

public enum P2pSourceDataStatusCode
{
	/// <summary>Block was successfully downloaded.</summary>
	Success,

	/// <summary>Operation was cancelled.</summary>
	Cancelled,

	/// <summary>There is no connected P2P node to download the block from.</summary>
	NoPeerAvailable,

	/// <summary>P2P node returned a block to us but it is not valid.</summary>
	InvalidBlockProvided,

	/// <summary>Failed to get block.</summary>
	/// <remarks>This covers for example networking errors.</remarks>
	Failure
}

public interface ISourceData
{
	Source Source { get; }
}

/// <summary>
/// Source data for a bitcoin block downloaded over the P2P Network.
/// </summary>
/// <param name="StatusCode">Description of the P2P operation result.</param>
/// <param name="Node">Node from which we downloaded some bitcoin block, or <c>null</c> if there was no node available.</param>
/// <param name="ConnectedNodes">
/// Number of connected peers at the moment when we downloaded the bitcoin block.
/// The number of connected peers can change at any moment.
/// </param>
public record P2pSourceData(P2pSourceDataStatusCode StatusCode, Node? Node, uint ConnectedNodes) : ISourceData
{
	public Source Source => Source.P2P;
}
