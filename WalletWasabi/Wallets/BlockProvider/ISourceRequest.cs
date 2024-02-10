using NBitcoin.Protocol;

namespace WalletWasabi.Wallets.BlockProvider;

public interface ISourceRequest { }

/// <summary>
/// Request to get a block from a trusted bitcoin full node.
/// </summary>
public record TrustedFullNodeSourceRequest() : ISourceRequest
{
	public static readonly TrustedFullNodeSourceRequest Instance = new();
}

/// <summary>
/// Request to get a block from a node from the P2P network.
/// </summary>
/// <param name="Node">Node from which to download a bitcoin block, or <c>null</c> to pick a P2P node randomly.</param>
/// <param name="Timeout">Timeout to download the block from the P2P node, or <c>null</c> to automatically select a timeout.</param>
/// <remarks>Automatic set both parameters to <c>null</c>, so the P2P node is chosen randomly and the timeout is automatically selected </remarks>
public record P2pSourceRequest(Node? Node, uint? Timeout) : ISourceRequest
{
	public static readonly P2pSourceRequest Automatic = new(Node: null, Timeout: null);
}
