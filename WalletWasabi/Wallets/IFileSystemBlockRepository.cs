using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Wallets.BlockProviders;

namespace WalletWasabi.Wallets;

/// <summary>
/// File-system based block repository that allows to retrieve and store blocks.
/// </summary>
public interface IFileSystemBlockRepository : IBlockProvider
{
	Task SaveAsync(Block element, CancellationToken cancellationToken);
}
