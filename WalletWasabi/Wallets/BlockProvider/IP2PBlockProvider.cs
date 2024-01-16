using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.Wallets.BlockProvider;

/// <summary>
/// P2P block provider downloads blocks from Bitcoin nodes using the P2P protocol.
/// </summary>
public interface IP2PBlockProvider : IBlockProvider
{
	/// <remarks>The implementations are not supposed to throw exceptions except <see cref="OperationCanceledException"/>.</remarks>
	Task<BlockWithSourceData?> TryGetBlockWithSourceDataAsync(uint256 blockHash, CancellationToken cancellationToken);
}
