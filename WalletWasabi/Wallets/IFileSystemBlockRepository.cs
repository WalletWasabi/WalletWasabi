using NBitcoin;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Wallets;

/// <summary>
/// File-system based block repository that allows to retrieve and store blocks.
/// </summary>
public interface IFileSystemBlockRepository
{
	Task<Block?> TryGetAsync(uint256 id, CancellationToken cancellationToken);

	Task SaveAsync(Block element, CancellationToken cancellationToken);

	Task RemoveAsync(uint256 id, CancellationToken cancellationToken);

	Task<int> CountAsync(CancellationToken cancellationToken);
}
