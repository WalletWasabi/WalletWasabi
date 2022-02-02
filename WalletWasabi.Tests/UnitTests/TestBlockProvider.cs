using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Wallets;

namespace WalletWasabi.Tests.UnitTests;

public class TestBlockProvider : IBlockProvider
{
	public TestBlockProvider(Dictionary<uint256, Block> blocks)
	{
		Blocks = blocks;
	}

	private Dictionary<uint256, Block> Blocks { get; }

	public Task<Block> GetBlockAsync(uint256 hash, CancellationToken cancel)
	{
		return Task.FromResult(Blocks[hash]);
	}
}
