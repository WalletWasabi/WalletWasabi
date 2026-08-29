using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;
using WalletWasabi.Tests.UnitTests.Mocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class FilterProvidersTests(ITestOutputHelper output)
{
	private static readonly byte[] DummyFilterData = Convert.FromHexString("02832810ec08a0");

	[Fact]
	public async Task BitcoinRpcProviderFetchesBoundedPageAndKeepsBestHeightAsync()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		var genesisHash = Network.Main.GetGenesis().GetHash();
		var blockHashes = Enumerable.Range(1, 250)
			.ToDictionary(x => x, x => new uint256((ulong)x));
		blockHashes[0] = genesisHash; 

		List<int> requestedBlockHeights = [];
		List<uint256> requestedFilterHashes = [];

		MockRpcClient rpc = new()
		{
			Network = Network.Main,
			OnGetBlockCountAsync = () => Task.FromResult(250),
			OnGetBlockHashAsync = height =>
			{
				requestedBlockHeights.Add(height);
				return Task.FromResult(blockHashes[height]);
			},
			OnGetBlockFilterAsync = blockHash =>
			{
				requestedFilterHashes.Add(blockHash);
				return Task.FromResult(CreateBlockFilter(blockHash));
			}
		};

		var provider = FilterProviders.CreateBitcoinRpcFilterProvider(rpc, new ConcurrentChain(Network.Main));
		var result = await provider(fromHeight: 0, fromHash: genesisHash, testCts.Token);

		Assert.True(result.IsOk);
		var newFilters = Assert.IsType<FiltersResponse.NewFiltersAvailable>(result.Value);
		Assert.Equal(250u, newFilters.BestHeight.Height);
		Assert.Equal(100, newFilters.Filters.Length);
		Assert.Equal(Enumerable.Range(0, 101), requestedBlockHeights);
		Assert.Equal(Enumerable.Range(1, 100).Select(x => blockHashes[x]), requestedFilterHashes);
		Assert.Equal(1u, newFilters.Filters[0].Header.Height.Height);
		Assert.Equal(100u, newFilters.Filters[^1].Header.Height.Height);
	}

	[Fact]
	public async Task BitcoinRpcProviderHandlesPartialBlockHashFailures()
	{
		var genesisHash = Network.Main.GetGenesis().GetHash();
		var blockHashes = Enumerable.Range(1, 100)
			.ToDictionary(x => x, x => new uint256((ulong)x));
		blockHashes[0] = genesisHash;

		MockRpcClient rpc = new()
		{
			Network = Network.Main,
			OnGetBlockCountAsync = () => Task.FromResult(100),
			OnGetBlockHashAsync = height =>
			{
				// Simulate reorg: blocks 51-100 no longer exist
				if (height > 50)
				{
					return Task.FromException<uint256>(
						new RPCException(RPCErrorCode.RPC_INVALID_PARAMETER, "Block height out of range", null));
				}
				return Task.FromResult(blockHashes[height]);
			},
			OnGetBlockFilterAsync = blockHash => Task.FromResult(CreateBlockFilter(blockHash))
		};

		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		var provider = FilterProviders.CreateBitcoinRpcFilterProvider(rpc, new ConcurrentChain(Network.Main));
		var result = await provider(fromHeight: 0, fromHash: genesisHash, testCts.Token);

		Assert.True(result.IsOk);
		var newFilters = Assert.IsType<FiltersResponse.NewFiltersAvailable>(result.Value);
		// Should only get 50 filters (the ones that succeeded)
		Assert.Equal(50, newFilters.Filters.Length);
		Assert.Equal(1u, newFilters.Filters[0].Header.Height.Height);
		Assert.Equal(50u, newFilters.Filters[^1].Header.Height.Height);
	}

	/// <summary>
	/// Reorg of the block at the height 100 representing the tip of the blockchain.
	/// </summary>
	[Fact(Timeout = 60_000)]
	public async Task BitcoinRpcProvider_ReorgDepth1Async()
	{
		var network = Network.Main;
		var blockchain = InitializeBlockchain(network);
		var rpcClient = CreateRpcMock(network, blockchain);

		var blockAtHeight99 = blockchain.GetBlock(99);
		var originalBlockAtHeight100 = blockchain.GetBlock(100);
		Assert.NotNull(originalBlockAtHeight100);

		var provider = FilterProviders.CreateBitcoinRpcFilterProvider(rpcClient, blockchain);

		// Sync blocks 0-100 of the original chain first.
		{
			var result = await provider(fromHeight: 0, fromHash: network.GetGenesis().GetHash(), TestContext.Current.CancellationToken);

			Assert.True(result.IsOk);
			Assert.IsType<FiltersResponse.NewFiltersAvailable>(result.Value);
		}

		// Simulate reorg of the chain at height 100 with a new block.
		//
		// Original: 0 <- 1 <- 2 <- ..<- 99 <- 100
		// New:      0 <- 1 <- 2 <- ..<- 99 <- 100  [REMOVED BLOCK AT HEIGHT 100]
		//                                \ <- 100* [NEW BLOCK BLOCK AT HEIGHT 100]
		{
			var header100 = CreateNewBlockHeader(network, blockAtHeight99, height: 100, forkBranch: true);
			var newBlockAtHeight100 = new ChainedBlock(header100, header100.GetHash(), blockAtHeight99);
			blockchain.SetTip(newBlockAtHeight100);

			output.WriteLine($"Block at height 99:      {blockAtHeight99.HashBlock}");
			output.WriteLine($"Old block at height 100: {originalBlockAtHeight100.HashBlock}");
			output.WriteLine($"New block at height 100: {newBlockAtHeight100.HashBlock}");
		}

		// Check that a reorg is detected.
		{
			var result = await provider(fromHeight: 100, fromHash: originalBlockAtHeight100.HashBlock, TestContext.Current.CancellationToken);

			Assert.True(result.IsOk);
			Assert.IsType<FiltersResponse.BestBlockUnknown>(result.Value);
		}
	}

	/// <summary>
	/// Reorg of the block at the height 100 representing the tip of the blockchain. Inferred from an RPC call, not from <see cref="ConcurrentChain"/>.
	/// </summary>
	[Fact(Timeout = 60_000)]
	public async Task BitcoinRpcProvider_ReorgDepth1DetectedLaterAsync()
	{
		var network = Network.Main;
		var blockchain = InitializeBlockchain(network);
		var rpcClient = CreateRpcMock(network, blockchain);

		var blockAtHeight99 = blockchain.GetBlock(99);
		var originalBlockAtHeight100 = blockchain.GetBlock(100);
		Assert.NotNull(originalBlockAtHeight100);

		var provider = FilterProviders.CreateBitcoinRpcFilterProvider(rpcClient, blockchain);

		// Sync blocks 0-100 of the original chain first.
		{
			var result = await provider(fromHeight: 0, fromHash: network.GetGenesis().GetHash(), TestContext.Current.CancellationToken);

			Assert.True(result.IsOk);
			Assert.IsType<FiltersResponse.NewFiltersAvailable>(result.Value);
		}

		// Reorg while getting block count.
		rpcClient.OnGetBlockCountAsync = () =>
		{
			// Simulate reorg of the chain at height 100 with a new block.
			//
			// Original: 0 <- 1 <- 2 <- ..<- 99 <- 100
			// New:      0 <- 1 <- 2 <- ..<- 99 <- 100          [REMOVED BLOCK AT HEIGHT 100]
			//                                \ <- 100* <- 101* [NEW BLOCK BLOCK AT HEIGHT 100 and 101]
			{
				var header100 = CreateNewBlockHeader(network, blockAtHeight99, height: 100, forkBranch: true);
				var newBlockAtHeight100 = new ChainedBlock(header100, header100.GetHash(), blockAtHeight99);
				blockchain.SetTip(newBlockAtHeight100);

				var header101 = CreateNewBlockHeader(network, newBlockAtHeight100, height: 101, forkBranch: true);
				var newBlockAtHeight101 = new ChainedBlock(header101, header101.GetHash(), newBlockAtHeight100);
				blockchain.SetTip(newBlockAtHeight101);

				output.WriteLine($"Old block at height 100: {originalBlockAtHeight100.HashBlock}");
				output.WriteLine($"New block at height 100: {newBlockAtHeight100.HashBlock}");
				output.WriteLine($"New block at height 101: {newBlockAtHeight101.HashBlock}");
			}

			Assert.Equal(101, blockchain.Height);

			return Task.FromResult(blockchain.Height);
		};

		// Check that a reorg is detected.
		{
			var result = await provider(fromHeight: 100, fromHash: originalBlockAtHeight100.HashBlock, TestContext.Current.CancellationToken);

			Assert.True(result.IsOk);
			Assert.IsType<FiltersResponse.BestBlockUnknown>(result.Value);
		}
	}

	/// <summary>
	/// Reorg of a block at the height 99 in a blockchain with the height 100. The last but one block is reorged here, not the tip.
	/// </summary>
	[Fact(Timeout = 60_000)]
	public async Task BitcoinRpcProvider_ReorgDepth2WithHugePowFor99thBlockAsync()
	{
		var network = Network.Main;
		var blockchain = InitializeBlockchain(network);
		var rpcClient = CreateRpcMock(network, blockchain);

		var blockAtHeight98 = blockchain.GetBlock(98);
		var originalBlockAtHeight99 = blockchain.GetBlock(99);
		var originalBlockAtHeight100 = blockchain.GetBlock(100);
		Assert.NotNull(originalBlockAtHeight99);
		Assert.NotNull(originalBlockAtHeight100);

		var provider = FilterProviders.CreateBitcoinRpcFilterProvider(rpcClient, blockchain);

		// Sync 0-100 on the original chain first.
		{
			var result = await provider(fromHeight: 0, fromHash: network.GetGenesis().GetHash(), TestContext.Current.CancellationToken);

			Assert.True(result.IsOk);
			Assert.IsType<FiltersResponse.NewFiltersAvailable>(result.Value);
		}

		// Simulate a deep reorg: block 99 is replaced by a competing block with more work that the original blocks at heights 99 and 100.
		//
		// Original: 0 <- .. <- 98 <- 99   <- 100
		// New:      0 <- .. <- 98 <- 99   <- 100  [BRANCH WITH ORPHANED LAST TWO BLOCKS]
		//                       \ <- 99*          [NEW WINNING BRANCH WITH HUGE POW (!)]
		ChainedBlock reorgTip;
		{
			var header99 = CreateNewBlockHeader(network, blockAtHeight98, height: 99, forkBranch: true);
			var newBlockAtHeight99 = new ChainedBlock(header99, header99.GetHash(), blockAtHeight98);

			reorgTip = newBlockAtHeight99;

			output.WriteLine($"Block at height 99  (orphaned): {originalBlockAtHeight99.HashBlock}  ");
			output.WriteLine($"Block at height 100 (orphaned): {originalBlockAtHeight100.HashBlock} (chain work: {originalBlockAtHeight100.GetChainWork(false)})");
			output.WriteLine($"Block at height 99  (new):      {newBlockAtHeight99.HashBlock} (chain work: {newBlockAtHeight99.GetChainWork(false)})");
		}

		blockchain.SetTip(reorgTip);

		var oldChainWork = originalBlockAtHeight100.GetChainWork(cacheResult: false);
		var newChainWork = reorgTip.GetChainWork(cacheResult: false);
		Assert.True(newChainWork > oldChainWork, "The single reorg block should carry more cumulative work than the entire orphaned two-block tail");

		Assert.Equal(99, blockchain.Height);
		Assert.NotEqual(originalBlockAtHeight99.HashBlock, blockchain.GetBlock(99)!.HashBlock);
		Assert.Null(blockchain.GetBlock(100));

		// Check that a reorg is detected.
		{
			var result = await provider(fromHeight: 100, fromHash: originalBlockAtHeight100.HashBlock, TestContext.Current.CancellationToken);

			Assert.True(result.IsOk);
			Assert.IsType<FiltersResponse.BestBlockUnknown>(result.Value);
		}
	}

	/// <summary>
	/// Initializes a blockchain with 100 blocks.
	/// </summary>
	private static ConcurrentChain InitializeBlockchain(Network network)
	{
		// Simulates our local blockchain view.
		var chainHeight = 100;
		var blockchain = new ConcurrentChain(network);

		// Fill real and local blockchains with blocks.
		var lastLocalTip = blockchain.Tip;
		for (var height = 1; height <= chainHeight; height++)
		{
			var header = CreateNewBlockHeader(network, lastLocalTip, height);
			lastLocalTip = new ChainedBlock(header, header.GetHash(), lastLocalTip);

			blockchain.SetTip(lastLocalTip);
		}

		Assert.Equal(chainHeight, blockchain.Height);

		return blockchain;
	}

	private static MockRpcClient CreateRpcMock(Network network, ConcurrentChain blockchain)
	{
		return new MockRpcClient()
		{
			Network = network,
			OnGetBlockCountAsync = () => Task.FromResult(blockchain.Height),
			OnGetBlockFilterAsync = blockHash => Task.FromResult(CreateBlockFilter(blockHash)),
			OnGetBlockHashAsync = height =>
			{
				var block = blockchain.GetBlock(height) ??
					throw new InvalidOperationException($"Block with height {height} is not present.");

				return Task.FromResult(block.HashBlock);
			}
		};
	}

	private static BlockHeader CreateNewBlockHeader(Network network, ChainedBlock tip, int height, bool forkBranch = false)
	{
		var header = network.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = tip.HashBlock;
		header.Nonce = (uint)height;

		if (forkBranch)
		{
			// Simulate that the block satisfies this target for chain work to be very high.
			header.Bits = new Target(uint256.Parse("0000000000000fff000000000000000000000000000000000000000000000000"));
		}

		return header;
	}

	private static BlockFilter CreateBlockFilter(uint256 blockHash) =>
		new(new GolombRiceFilter(DummyFilterData, 20, 1 << 20), blockHash);
}
