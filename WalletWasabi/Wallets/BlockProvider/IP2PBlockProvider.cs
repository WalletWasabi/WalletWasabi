using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;

namespace WalletWasabi.Wallets.BlockProvider;

/// <summary>
/// P2P block provider downloads blocks from Bitcoin nodes using the P2P protocol.
/// </summary>
public interface IP2PBlockProvider : IBlockProvider
{
	/// <summary>
	/// <see cref="Node"/> and timeout are picked automatically for you.
	/// </summary>
	/// <inheritdoc cref="TryGetBlockWithSourceDataAsync(uint256, Node, double, CancellationToken)"/>
	Task<P2pBlockResponse> TryGetBlockWithSourceDataAsync(uint256 blockHash, P2pSourceRequest sourceRequest, CancellationToken cancellationToken);
}
