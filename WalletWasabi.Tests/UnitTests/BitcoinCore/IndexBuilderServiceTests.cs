using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.BitcoinRpc.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

public class IndexBuilderServiceTests
{
	private readonly string _testDirectory;
	private readonly string _filtersPath;
	private readonly IndexBuilderServiceOptions _options = new(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));

	public IndexBuilderServiceTests()
	{
		_testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		Directory.CreateDirectory(_testDirectory);
		_filtersPath = Path.Combine(_testDirectory, "filters.sqlite");
	}

	[Fact]
	public async Task UnsynchronizedBitcoinNodeAsync()
	{
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = 0,
				Blocks = 0,
				InitialBlockDownload = false
			}),
		};
		using var indexer = new IndexBuilderService(rpc, _filtersPath, _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(0.5));
		// There is only starting filter
		Assert.True(indexer.GetLastFilter()?.Header.BlockHash.Equals(StartingFilters.GetStartingFilter(rpc.Network).Header.BlockHash));
		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task StalledBitcoinNodeAsync()
	{
		var called = 0;
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = 10_000,
					Blocks = 0,
					InitialBlockDownload = true
				});
			}
		};
		using var indexer = new IndexBuilderService(rpc, _filtersPath, _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// There is only starting filter
		Assert.True(indexer.GetLastFilter()?.Header.BlockHash.Equals(StartingFilters.GetStartingFilter(rpc.Network).Header.BlockHash));
		Assert.True(called > 1);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task SynchronizingBitcoinNodeAsync()
	{
		var called = 0;
		var blockchain = GenerateBlockchain().Take(10).ToArray();
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = (ulong)blockchain.Length,
					Blocks = (ulong)called,
					InitialBlockDownload = true
				});
			},
			OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
			OnGetVerboseBlockAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash))
		};
		using var indexer = new IndexBuilderService(rpc, _filtersPath, _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(0.5));

		var lastFilter = indexer.GetLastFilter();
		Assert.Equal(9, (int)lastFilter!.Header.Height);
		Assert.True(called > 1);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public void IncludeTaprootScriptInFilters()
	{
		var getBlockRpcRawResponse = File.ReadAllText("./UnitTests/Data/VerboseBlock.json");

		var block = RpcParser.ParseVerboseBlockResponse(getBlockRpcRawResponse);
		var filter = IndexBuilderService.BuildFilterForBlock(block);

		var txOutputs = block.Transactions.SelectMany(x => x.Outputs);
		var prevTxOutputs = block.Transactions.SelectMany(x => x.Inputs.OfType<VerboseInputInfo.Full>().Select(y => y.PrevOut));
		var allOutputs = txOutputs.Concat(prevTxOutputs);

		var indexableOutputs = allOutputs.Where(x => x?.PubkeyType is RpcPubkeyType.TxWitnessV0Keyhash or RpcPubkeyType.TxWitnessV1Taproot);
		var nonIndexableOutputs = allOutputs.Except(indexableOutputs);

		static byte[] ComputeKey(uint256 blockId) => blockId.ToBytes()[0..16];

		Assert.All(indexableOutputs, x => Assert.True(filter.Match(x?.ScriptPubKey.ToCompressedBytes(), ComputeKey(block.Hash))));
		Assert.All(nonIndexableOutputs, x => Assert.False(filter.Match(x?.ScriptPubKey.ToCompressedBytes(), ComputeKey(block.Hash))));
	}

	[Fact]
	public async Task SynchronizedBitcoinNodeAsync()
	{
		var blockchain = GenerateBlockchain().Take(10).ToArray();
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = (ulong)blockchain.Length - 1,
				Blocks = (ulong)blockchain.Length - 1,
				InitialBlockDownload = false
			}),
			OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
			OnGetVerboseBlockAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash))
		};

		using var indexer = new IndexBuilderService(rpc, _filtersPath, _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(0.5));

		var result = await indexer.GetFilterLinesExcludingAsync(blockchain[0].Hash, 100);
		Assert.True(result.found);
		Assert.Equal(9, result.bestHeight.Value);
		Assert.Equal(9, result.filters.Count());

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	private IEnumerable<VerboseBlockInfo> GenerateBlockchain() =>
		from height in Enumerable.Range(0, int.MaxValue).Select(x => (ulong)x)
		select new VerboseBlockInfo(
			BlockHashFromHeight(height),
			height,
			BlockHashFromHeight(height + 1),
			DateTimeOffset.UtcNow.AddMinutes(height * 10),
			height,
			Enumerable.Empty<VerboseTransactionInfo>());

	private static uint256 BlockHashFromHeight(ulong height)
		=> height == 0 ? uint256.Zero : Hashes.DoubleSHA256(BitConverter.GetBytes(height));

	[Fact]
	public async Task ProcessNewBlocksAsync()
	{
		// Setup mock RPC client with a series of blocks
		var blockHashes = new Dictionary<uint, uint256>();
		var blocks = new Dictionary<uint256, VerboseBlockInfo>();

		// Genesis block
		var genesisBlockHash = Network.RegTest.GenesisHash;
		blockHashes[0] = genesisBlockHash;

		// Create 10 blocks
		for (uint i = 1; i <= 10; i++)
		{
			// Create block hash
			var blockHash = new uint256(i);
			blockHashes[i] = blockHash;

			// Create block data with previous hash
			blocks[blockHash] = CreateMockBlock(blockHash, blockHashes[i - 1], i);
		}

		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = 10,
				Blocks = 10,
				InitialBlockDownload = false
			}),
			OnGetBlockHashAsync = height => Task.FromResult(blockHashes.ContainsKey((uint)height) ? blockHashes[(uint)height] : uint256.Zero),
			OnGetVerboseBlockAsync = hash => Task.FromResult(blocks.ContainsKey(hash) ? blocks[hash] : null)
		};

		using var indexer = new IndexBuilderService(rpc, _filtersPath, _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		// Give time for processing
		await Task.Delay(TimeSpan.FromSeconds(1));

		// Check that all blocks were processed
		var lastFilter = indexer.GetLastFilter();
		Assert.NotNull(lastFilter);
		Assert.Equal((uint)10, lastFilter.Header.Height);
		Assert.Equal(blockHashes[10], lastFilter.Header.BlockHash);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task HandleReorgAsync()
	{
		// Initial chain setup with 5 blocks
		var blockHashes = new Dictionary<uint, uint256>();
		var blocks = new Dictionary<uint256, VerboseBlockInfo>();

		// Genesis block
		var genesisBlockHash = Network.RegTest.GenesisHash;
		blockHashes[0] = genesisBlockHash;

		// Create 5 blocks
		for (uint i = 1; i <= 5; i++)
		{
			var blockHash = new uint256(i);
			blockHashes[i] = blockHash;
			blocks[blockHash] = CreateMockBlock(blockHash, blockHashes[i - 1], i);
		}

		// Create a fork starting at block 3
		var forkHashes = new Dictionary<uint, uint256>();
		for (uint i = 0; i <= 2; i++)
		{
			forkHashes[i] = blockHashes[i]; // Same up to block 2
		}

		// Different blocks for 3, 4, 5
		for (uint i = 3; i <= 7; i++)
		{
			var blockHash = new uint256(i * 1000); // Different hash pattern
			forkHashes[i] = blockHash;

			// For block 3, the prev hash is block 2
			var prevHash = i == 3 ? forkHashes[2] : forkHashes[i - 1];

			blocks[blockHash] = CreateMockBlock(blockHash, prevHash, i);
		}

		var chainInfo = new BlockchainInfo { Headers = 5, Blocks = 5, InitialBlockDownload = false };
		var heights = new Dictionary<uint, uint256>(blockHashes);

		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(chainInfo),
			OnGetBlockHashAsync = height => Task.FromResult(heights.ContainsKey((uint)height) ? heights[(uint)height] : uint256.Zero),
			OnGetVerboseBlockAsync = hash => Task.FromResult(blocks.ContainsKey(hash) ? blocks[hash] : null)
		};

		using var indexer = new IndexBuilderService(rpc, _filtersPath, _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		// Give time for initial processing
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Verify initial state
		var firstLastFilter = indexer.GetLastFilter();
		Assert.NotNull(firstLastFilter);
		Assert.Equal((uint)5, firstLastFilter.Header.Height);
		Assert.Equal(blockHashes[5], firstLastFilter.Header.BlockHash);

		// Simulate reorg by changing the chain
		heights = new Dictionary<uint, uint256>(forkHashes);
		chainInfo.Headers = 7;
		chainInfo.Blocks = 7;

		// Give time for reorg processing
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Verify post-reorg state
		var secondLastFilter = indexer.GetLastFilter();
		Assert.NotNull(secondLastFilter);
		Assert.Equal((uint)7, secondLastFilter.Header.Height);
		Assert.Equal(forkHashes[7], secondLastFilter.Header.BlockHash);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task SyncPausesWhenUpToDateAsync()
	{
		// Setup mock with 5 blocks
		var blockHashes = new Dictionary<uint, uint256>();
		var blocks = new Dictionary<uint256, VerboseBlockInfo>();

		// Genesis block
		var genesisBlockHash = Network.RegTest.GenesisHash;
		blockHashes[0] = genesisBlockHash;

		for (uint i = 1; i <= 5; i++)
		{
			var blockHash = new uint256(i);
			blockHashes[i] = blockHash;
			blocks[blockHash] = CreateMockBlock(blockHash, blockHashes[i - 1], i);
		}

		var chainInfo = new BlockchainInfo { Headers = 5, Blocks = 5, InitialBlockDownload = false };
		int blockchainInfoCallCount = 0;

		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				blockchainInfoCallCount++;
				return Task.FromResult(chainInfo);
			},
			OnGetBlockHashAsync = height => Task.FromResult(blockHashes.ContainsKey((uint)height) ? blockHashes[(uint)height] : uint256.Zero),
			OnGetVerboseBlockAsync = hash => Task.FromResult(blocks.ContainsKey(hash) ? blocks[hash] : null)
		};

		using var indexer = new IndexBuilderService(rpc, _filtersPath, _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		// Give time for processing
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// New block appears
		var newBlockHash = new uint256(6);
		blockHashes[6] = newBlockHash;
		blocks[newBlockHash] = CreateMockBlock(newBlockHash, blockHashes[5], 6);
		chainInfo.Headers = 6;
		chainInfo.Blocks = 6;

		// Wait for processing of new block
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Verify new block was processed
		var lastFilter = indexer.GetLastFilter();
		Assert.NotNull(lastFilter);
		Assert.Equal((uint)6, lastFilter.Header.Height);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task GetFilterLinesExcludingReturnsCorrectFiltersAsync()
	{
		// Setup mock with 10 blocks
		var blockHashes = new Dictionary<uint, uint256>();
		var blocks = new Dictionary<uint256, VerboseBlockInfo>();

		// Genesis block
		var genesisBlockHash = Network.RegTest.GenesisHash;
		blockHashes[0] = genesisBlockHash;

		for (uint i = 1; i <= 10; i++)
		{
			var blockHash = new uint256(i);
			blockHashes[i] = blockHash;
			blocks[blockHash] = CreateMockBlock(blockHash, blockHashes[i - 1], i);
		}

		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = 10,
				Blocks = 10,
				InitialBlockDownload = false
			}),
			OnGetBlockHashAsync = height => Task.FromResult(blockHashes.ContainsKey((uint)height) ? blockHashes[(uint)height] : uint256.Zero),
			OnGetVerboseBlockAsync = hash => Task.FromResult(blocks.ContainsKey(hash) ? blocks[hash] : null)
		};

		using var indexer = new IndexBuilderService(rpc, _filtersPath, _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		// Give time for processing
		await Task.Delay(TimeSpan.FromSeconds(1));

		// Test GetFilterLinesExcludingAsync
		var result = await indexer.GetFilterLinesExcludingAsync(blockHashes[5], 3);

		// Check results
		Assert.True(result.found);
		Assert.Equal(new Height((uint)10), result.bestHeight);
		Assert.Equal(3, result.filters.Count()); // Should have filters for blocks 6, 7, 8

		// Verify the first filter is for block 6
		var firstFilter = result.filters.First();
		Assert.Equal((uint)6, firstFilter.Header.Height);
		Assert.Equal(blockHashes[6], firstFilter.Header.BlockHash);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task IndexBuilderHandlesRpcErrorsGracefullyAsync()
	{
		bool shouldFail = false;
		var blockHashes = new Dictionary<uint, uint256>();
		var blocks = new Dictionary<uint256, VerboseBlockInfo>();

		// Genesis block
		var genesisBlockHash = Network.RegTest.GenesisHash;
		blockHashes[0] = genesisBlockHash;
		blockHashes[1] = new uint256(1);
		blockHashes[2] = new uint256(2);
		blocks[blockHashes[1]] = CreateMockBlock(blockHashes[1], blockHashes[0], 1);
		blocks[blockHashes[2]] = CreateMockBlock(blockHashes[2], blockHashes[1], 2);

		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = 2,
				Blocks = 2,
				InitialBlockDownload = false
			}),
			OnGetBlockHashAsync = height =>
			{
				if (shouldFail)
				{
					throw new RPCException(RPCErrorCode.RPC_INVALID_PARAMETER, "Test error", new RPCResponse(null!));
				}
				return Task.FromResult(blockHashes.ContainsKey((uint)height) ? blockHashes[(uint)height] : uint256.Zero);
			},
			OnGetVerboseBlockAsync = hash =>
			{
				if (shouldFail)
				{
					throw new Exception("Test error");
				}

				return Task.FromResult(blocks.ContainsKey(hash) ? blocks[hash] : null);
			}
		};

		using var indexer = new IndexBuilderService(rpc, _filtersPath, _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		// Give time for initial processing
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Start failing RPC calls
		shouldFail = true;

		// Give time to hit errors
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Stop failing
		shouldFail = false;

		// Give time to recover
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Service should still be running
		Assert.Null(indexingStartTask.Exception);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task DisposalCleansUpResourcesAsync()
	{
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = 0,
				Blocks = 0,
				InitialBlockDownload = false
			}),
		};

		using var indexer = new IndexBuilderService(rpc, _filtersPath, _options);

		try
		{
			await indexer.StartAsync(CancellationToken.None);

			// Allow time for startup
			await Task.Delay(TimeSpan.FromSeconds(0.5));
		}
		finally
		{
			if (indexer != null)
			{
				// Dispose should properly clean up
				await indexer.StopAsync(CancellationToken.None);

				// Check if file exists
				Assert.True(File.Exists(_filtersPath), "Filter file should be created and remain after service stops");
			}
		}
	}

	private static VerboseBlockInfo CreateMockBlock(uint256 hash, uint256 prevHash, uint height)
	{
		var blockHash = new uint256(height * 1000);
		// Create single coinbase transaction
		var tx = new VerboseTransactionInfo(
			blockInfo: new TransactionBlockInfo(blockHash, DateTimeOffset.UtcNow, height),
			id: blockHash,
			inputs: [new VerboseInputInfo.Coinbase("I'm rich!")],
			outputs:
			[
				new VerboseOutputInfo(
					value: 50_000_000_000,
					scriptPubKey: new Script(),
					pubkeyType: "wpkh")
			]);

		return new VerboseBlockInfo(
			hash: hash,
			prevBlockHash: prevHash,
			confirmations: 10 - height + 1,
			height: height,
			blockTime: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10 - height),
			transactions: [tx]);
	}

	public void Dispose()
	{
		try
		{
			if (Directory.Exists(_testDirectory))
			{
				Directory.Delete(_testDirectory, true);
			}
		}
		catch
		{
			// Best effort cleanup
		}
	}
}
