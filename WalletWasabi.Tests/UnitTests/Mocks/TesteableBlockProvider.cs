using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Wallets;

namespace WalletWasabi.Tests.UnitTests.Mocks;

class TesteableBlockProvider
{
	public Func<uint256, CancellationToken, Task<Block?>> OnTryGetBlockAsync { get; set; }
	public Task<Block?> TryGetBlockAsync(uint256 blockHash, CancellationToken cancellationToken) =>
		OnTryGetBlockAsync.Invoke(blockHash, cancellationToken);
}
