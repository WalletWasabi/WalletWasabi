using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Wallets;

namespace WalletWasabi.Tests.UnitTests.Mocks;

class TesteableFileSystemBlockRepository : IFileSystemBlockRepository
{
	public Func<uint256, CancellationToken, Task<Block?>> OnTryGetBlockAsync { get; set; }
	public Func<Block, CancellationToken, Task> OnSaveAsync { get; set; }
	public Func<uint256, CancellationToken, Task> OnRemoveAsync { get; set; }

	public Task<Block?> TryGetAsync(uint256 blockHash, CancellationToken cancellationToken) =>
		OnTryGetBlockAsync.Invoke(blockHash, cancellationToken);

	public Task SaveAsync(Block block, CancellationToken cancellationToken) =>
		OnSaveAsync.Invoke(block, cancellationToken);

	public Task RemoveAsync(uint256 blockHash, CancellationToken cancellationToken) =>
		OnRemoveAsync.Invoke(blockHash, cancellationToken);

	public Task<int> CountAsync(CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
}
