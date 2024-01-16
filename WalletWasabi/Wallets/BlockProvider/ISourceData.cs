using NBitcoin.Protocol;

namespace WalletWasabi.Wallets.BlockProvider;

/// <summary>
/// Source that provided a bitcoin block to us.
/// </summary>
[Flags]
public enum Source 
{
	/// <summary>Wasabi Wallet file-system cache.</summary>
	/// <remarks>The assumption is that the cache is tested always before real block sources.</remarks>
	FileSystemCache = 0,

	/// <summary>Trusted full node that can provide blocks and there is no concern over privacy.</summary>
	TrustedFullNode = 1,

	/// <summary>P2P node that provided a block to us.</summary>
	P2P = 2,

	/// <summary>All available sources with trusted full nodes being asked first and only then fallback to P2P.</summary>
	All = TrustedFullNode | P2P,
}

public interface ISourceData
{
	Source Source { get; }
}

/// <summary>
/// Source data with no additional information.
/// </summary>
public record EmptySourceData(Source Source) : ISourceData;

/// <summary>
/// Source data for a bitcoin block downloaded over P2P.
/// </summary>
/// <param name="Node">Node from which we downloaded some bitcoin block, or <c>null</c> if there was no node available.</param>
/// <param name="ConnectedNodes">
/// Number of connected peers at the moment when we downloaded the bitcoin block.
/// The number of connected peers can change at any moment.
/// </param>
public record P2pSourceData(Node? Node, uint ConnectedNodes) : ISourceData {

	public Source Source => Source.P2P;
}
