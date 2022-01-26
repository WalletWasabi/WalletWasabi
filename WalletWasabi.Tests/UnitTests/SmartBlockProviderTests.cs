using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class SmartBlockProviderTests
{
	/// <summary>
	/// Tests <see cref="SmartBlockProvider.GetBlockAsync(uint256, CancellationToken)"/> behavior.
	/// </summary>
	[Fact]
	public async void TestAsync()
	{
		var blocks = new Dictionary<uint256, Block>
		{
			[uint256.Zero] = Network.Main.Consensus.ConsensusFactory.CreateBlock(),
			[uint256.One] = Network.Main.Consensus.ConsensusFactory.CreateBlock(),
		};

		var blockProvider = new TestBlockProvider(blocks);
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var smartBlockProvider = new SmartBlockProvider(blockProvider, cache);

		Task<Block> b0 = smartBlockProvider.GetBlockAsync(uint256.Zero, CancellationToken.None);
		Task<Block> b1 = smartBlockProvider.GetBlockAsync(uint256.One, CancellationToken.None);
		Task<Block> b2 = smartBlockProvider.GetBlockAsync(uint256.Zero, CancellationToken.None);

		// Wait for all blocks to be fetched.
		Block[] result = await Task.WhenAll(b0, b1, b2);

		// We assert here that SmartBlockProvider used internal BlockProvider to get blocks
		// and that those blocks correspond to expected blocks.
		Assert.Equal(blocks[0], result[0]);
		Assert.Equal(blocks[0], result[2]);

		Assert.NotEqual(blocks[0], result[1]);
	}
}
