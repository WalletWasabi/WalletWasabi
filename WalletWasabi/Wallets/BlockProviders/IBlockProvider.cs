using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.Wallets.BlockProviders;

public interface IBlockProvider
{
	Task<Block?> TryGetBlockAsync(uint256 blockHash, CancellationToken cancellationToken);
}
