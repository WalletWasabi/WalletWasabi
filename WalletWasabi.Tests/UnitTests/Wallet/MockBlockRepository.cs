using NBitcoin;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using WalletWasabi.Wallets;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class MockBlockRepository : IRepository<uint256, Block>
{
	public MockBlockRepository(Dictionary<uint256, Block> blocks)
	{
		Blocks = blocks;
	}

	public Dictionary<uint256, Block> Blocks { get; }

	public Task<Block?> TryGetAsync(uint256 id, CancellationToken cancel) =>
		Task.FromResult(Blocks.GetValueOrDefault(id));

	public Task SaveAsync(Block element, CancellationToken cancel) =>
		Task.CompletedTask;

	public Task RemoveAsync(uint256 id, CancellationToken cancel) =>
		Task.CompletedTask;

	public Task<int> CountAsync(CancellationToken cancel) =>
		Task.FromResult(Blocks.Count);
}
