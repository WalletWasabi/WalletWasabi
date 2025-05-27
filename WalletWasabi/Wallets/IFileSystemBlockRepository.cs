using NBitcoin;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Wallets;

/// <summary>
/// File-system based block repository that allows to retrieve and store blocks.
/// </summary>
public interface IFileSystemBlockRepository
{
	Task<Block?> TryGetBlockAsync(uint256 blockHash, CancellationToken cancellationToken);
	Task SaveAsync(Block element, CancellationToken cancellationToken);
}
