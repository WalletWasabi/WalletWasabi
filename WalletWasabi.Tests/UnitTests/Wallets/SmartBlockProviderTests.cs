using Microsoft.Extensions.Caching.Memory;
using Moq;
using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.UnitTests.Wallet;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallets;

/// <summary>
/// Tests for <see cref="SmartBlockProvider"/>.
/// </summary>
public class SmartBlockProviderTests
{
	/// <summary>
	/// Tests <see cref="SmartBlockProvider.GetBlockAsync(uint256, CancellationToken)"/> behavior.
	/// </summary>
	[Fact]
	public async void GettingBlocksFromP2pTestAsync()
	{
		var emptyDict = new Dictionary<uint256, Block>();
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(1));

		// Dummy block repository that does not gets (or stores) anything from the file-storage.
		MockFileSystemBlockRepository mockBlockRepository = new(emptyDict);

		// Rpc block provider returns nothing. Simulate that it's not enabled.
		var mockRpcBlockProvider = new TestBlockProvider(emptyDict);

		// Local block provider returns nothing. Simulate that it's not enabled.
		var mockLocalBlockProvider = new TestBlockProvider(emptyDict);

		// P2P block provider with predefined blocks.
		Dictionary<uint256, Block> blocks = new()
		{
			[uint256.Zero] = Network.Main.Consensus.ConsensusFactory.CreateBlock(),
			[uint256.One] = Network.Main.Consensus.ConsensusFactory.CreateBlock(),
		};

		TestBlockProvider p2PBlockProvider = new(blocks);

		using MemoryCache cache = new(new MemoryCacheOptions());
		IBlockProvider smartBlockProvider = new SmartBlockProvider(
			mockBlockRepository,
			rpcBlockProvider: mockRpcBlockProvider,
			specificNodeBlockProvider: mockLocalBlockProvider,
			p2PBlockProvider: p2PBlockProvider,
			cache);

		Task<Block> b0 = smartBlockProvider.GetBlockAsync(uint256.Zero, testDeadlineCts.Token);
		Task<Block> b1 = smartBlockProvider.GetBlockAsync(uint256.One, testDeadlineCts.Token);
		Task<Block> b2 = smartBlockProvider.GetBlockAsync(uint256.Zero, testDeadlineCts.Token);

		// Wait for all blocks to be fetched.
		Block[] result = await Task.WhenAll(b0, b1, b2).WaitAsync(testDeadlineCts.Token);

		// Assert that SmartBlockProvider uses an internal block provider to get blocks and that those blocks correspond to expected blocks.
		Assert.Same(blocks[0], result[0]);
		Assert.Same(blocks[0], result[2]);

		Assert.NotSame(blocks[0], result[1]);
	}

	private class TestBlockProvider : IBlockProvider
	{
		public TestBlockProvider(Dictionary<uint256, Block> blocks)
		{
			Blocks = blocks;
		}

		private Dictionary<uint256, Block> Blocks { get; }

		public Task<Block?> TryGetBlockAsync(uint256 hash, CancellationToken cancel)
		{
			return Task.FromResult<Block?>(Blocks.GetValueOrDefault(hash));
		}
	}
}
