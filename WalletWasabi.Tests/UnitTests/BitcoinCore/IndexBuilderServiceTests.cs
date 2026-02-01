using System.Collections.Concurrent;
using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.BitcoinRpc.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Models;
using WalletWasabi.Tests.UnitTests.Wallet;
using Xunit;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

public class IndexBuilderServiceTests
{
	private readonly string _testDirectory;
	private readonly string _filtersPath;
	private readonly ITestOutputHelper _testOutputHelper;
	private readonly IndexBuilderServiceOptions _options = new(
		DelayForNodeToCatchUp: TimeSpan.FromMilliseconds(50),
		DelayAfterEverythingIsDone: TimeSpan.FromMilliseconds(50),
		DelayInCaseOfError: TimeSpan.FromMilliseconds(50));

	public IndexBuilderServiceTests(ITestOutputHelper testOutputHelper)
	{
		_testOutputHelper = testOutputHelper;
		_testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		Directory.CreateDirectory(_testDirectory);
		_filtersPath = Path.Combine(_testDirectory, "filters.sqlite");
	}

	[Fact]
	public async Task UnsynchronizedBitcoinNodeAsync()
	{
		var node = await MockNode.CreateNodeAsync(new MockNodeOptions(BlockToGenerate: 0));
		var rpc = node.Rpc;
		using var indexer = new IndexBuilderService(rpc, _filtersPath, options: _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(0.5));
		// There is only starting filter
		var lastFilter = await indexer.GetLastFilterAsync(CancellationToken.None);
		Assert.True(lastFilter?.Header.BlockHash.Equals(StartingFilters.GetStartingFilter(rpc.Network).Header.BlockHash));
		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task StalledBitcoinNodeAsync()
	{
		var node = await MockNode.CreateNodeAsync(new MockNodeOptions(BlockToGenerate: 0));
		var rpc = node.Rpc;
		var called = 0;
		rpc.OnGetBlockchainInfoAsync = () =>
		{
			called++;
			return Task.FromResult(new BlockchainInfo
			{
				Headers = 10_000,
				Blocks = 0,
				InitialBlockDownload = true
			});
		};
		using var indexer = new IndexBuilderService(rpc, _filtersPath, options: _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// There is only starting filter
		var lastFilter = await indexer.GetLastFilterAsync(CancellationToken.None);
		Assert.True(lastFilter?.Header.BlockHash.Equals(StartingFilters.GetStartingFilter(rpc.Network).Header.BlockHash));
		Assert.True(called > 1);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task SynchronizingBitcoinNodeAsync()
	{
		var node = await MockNode.CreateNodeAsync(new MockNodeOptions(BlockToGenerate: 10));
		var rpc = node.Rpc;
		var called = 0;
		rpc.OnGetBlockchainInfoAsync = () =>
		{
			called++;
			return Task.FromResult(new BlockchainInfo
			{
				Headers = (ulong) node.BlockChain.Count,
				Blocks = (ulong) called,
				InitialBlockDownload = true
			});
		};
		using var indexer = new IndexBuilderService(rpc, _filtersPath, options: _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(0.5));

		var lastFilter = await indexer.GetLastFilterAsync(CancellationToken.None);
		Assert.Equal(10, (int)lastFilter!.Header.Height);
		Assert.True(called > 1);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public void IncludeTaprootScriptInFilters()
	{
		var getBlockRpcRawResponse = File.ReadAllText("./UnitTests/Data/VerboseBlock.json");

		var block = RpcParser.ParseVerboseBlockResponse(getBlockRpcRawResponse);
		var filter = LegacyWasabiFilterGenerator.BuildFilterForBlock(block);

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
		var node = await MockNode.CreateNodeAsync(new MockNodeOptions(BlockToGenerate: 10));

		using var indexer = new IndexBuilderService(node.Rpc, _filtersPath, options: _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(0.5));

		var result = await indexer.GetFilterLinesExcludingAsync(node.BlockChain.Keys.First(), 100);
		Assert.True(result.found);
		Assert.Equal(10, result.bestHeight.Value);
		Assert.Equal(10, result.filters.Count());

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task ProcessNewBlocksAsync()
	{
		var node = await MockNode.CreateNodeAsync(new MockNodeOptions(BlockToGenerate: 10));

		using var indexer = new IndexBuilderService(node.Rpc, _filtersPath, options: _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		// Give time for processing
		await Task.Delay(TimeSpan.FromSeconds(1));

		// Check that all blocks were processed
		var bestBlockHash = await node.Rpc.GetBestBlockHashAsync();
		var lastFilter = await indexer.GetLastFilterAsync(CancellationToken.None);
		Assert.NotNull(lastFilter);
		Assert.Equal((uint)10, lastFilter.Header.Height);
		Assert.Equal(bestBlockHash, lastFilter.Header.BlockHash);

		// Generate a new block
		await node.GenerateBlockAsync(CancellationToken.None);
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Check that the new block was processed
		bestBlockHash = await node.Rpc.GetBestBlockHashAsync();
		lastFilter = await indexer.GetLastFilterAsync(CancellationToken.None);
		Assert.NotNull(lastFilter);
		Assert.Equal((uint)11, lastFilter.Header.Height);
		Assert.Equal(bestBlockHash, lastFilter.Header.BlockHash);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task HandleReorgAsync()
	{
		var node = await MockNode.CreateNodeAsync(new MockNodeOptions(BlockToGenerate: 5));

		using var indexer = new IndexBuilderService(node.Rpc, _filtersPath, options: _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		// Give time for initial processing
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Verify initial state
		var bestBlockHash = await node.Rpc.GetBestBlockHashAsync();
		var firstLastFilter = await indexer.GetLastFilterAsync(CancellationToken.None);
		Assert.NotNull(firstLastFilter);
		Assert.Equal((uint)5, firstLastFilter.Header.Height);
		Assert.Equal(bestBlockHash, firstLastFilter.Header.BlockHash);

		// Simulate reorg by changing the chain
		node.BlockChain.Remove(bestBlockHash);
		bestBlockHash = await node.Rpc.GetBestBlockHashAsync();
		node.BlockChain.Remove(bestBlockHash);
		using Key dummyKey = new();
		await node.Rpc.GenerateToAddressAsync(4, dummyKey.GetAddress(ScriptPubKeyType.Segwit, Network.RegTest));

		// Give time for reorg processing
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Verify post-reorg state
		bestBlockHash = await node.Rpc.GetBestBlockHashAsync();
		var secondLastFilter =  await indexer.GetLastFilterAsync(CancellationToken.None);

		Assert.NotNull(secondLastFilter);
		Assert.Equal((uint)7, secondLastFilter.Header.Height);
		Assert.Equal(bestBlockHash, secondLastFilter.Header.BlockHash);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task SyncPausesWhenUpToDateAsync()
	{
		var node = await MockNode.CreateNodeAsync(new MockNodeOptions(BlockToGenerate: 10));
		var indexBuilderOptions = _options with {DelayAfterEverythingIsDone = TimeSpan.FromSeconds(10)};
		using var indexer = new IndexBuilderService(node.Rpc, _filtersPath, options: indexBuilderOptions);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		// Give time for processing
		await Task.Delay(TimeSpan.FromSeconds(1));

		// Check that all blocks were processed
		var bestBlockHash = await node.Rpc.GetBestBlockHashAsync();
		var lastFilter = await indexer.GetLastFilterAsync(CancellationToken.None);
		Assert.NotNull(lastFilter);
		Assert.Equal((uint)10, lastFilter.Header.Height);
		Assert.Equal(bestBlockHash, lastFilter.Header.BlockHash);

		bool neverCalled = true;
		var continuation = node.Rpc.OnGetBlockchainInfoAsync!;
		node.Rpc.OnGetBlockchainInfoAsync = () =>
		{
			neverCalled = false;
			return continuation();
		};
		// Generate a new block
		await node.GenerateBlockAsync(CancellationToken.None);
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// The service is still sleeping
		Assert.True(neverCalled);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task GetFilterLinesExcludingReturnsCorrectFiltersAsync()
	{
		// Setup mock with 10 blocks
		var node = await MockNode.CreateNodeAsync(new MockNodeOptions(BlockToGenerate: 10));
		using var indexer = new IndexBuilderService(node.Rpc, _filtersPath, options: _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		// Give time for processing
		await Task.Delay(TimeSpan.FromSeconds(1));

		// Test GetFilterLinesExcludingAsync
		var blockHash = await node.Rpc.GetBlockHashAsync(5);
		var result = await indexer.GetFilterLinesExcludingAsync(blockHash, 3);

		// Check results
		Assert.True(result.found);
		Assert.Equal(new Height((uint)10), result.bestHeight);
		Assert.Equal(3, result.filters.Count()); // Should have filters for blocks 6, 7, 8

		// Verify the first filter is for block 6
		blockHash = await node.Rpc.GetBlockHashAsync(6);
		var firstFilter = result.filters.First();
		Assert.Equal((uint)6, firstFilter.Header.Height);
		Assert.Equal(blockHash, firstFilter.Header.BlockHash);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task IndexBuilderHandlesRpcErrorsGracefullyAsync()
	{
		var node = await MockNode.CreateNodeAsync(new MockNodeOptions(BlockToGenerate: 2));

		using var indexer = new IndexBuilderService(node.Rpc, _filtersPath, options: _options);
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		// Give time for initial processing
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Make GetBlockchainInfo rpc calls to fail
		var onGetBlockchainInfoAsyncFunc = node.Rpc.OnGetBlockchainInfoAsync;
		node.Rpc.OnGetBlockchainInfoAsync = () =>
			throw new RPCException(RPCErrorCode.RPC_OUT_OF_MEMORY, "", new RPCResponse(null!));

		// Give time to hit errors
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Make GetBlock rpc calls to fail
		node.Rpc.OnGetBlockchainInfoAsync = onGetBlockchainInfoAsyncFunc; // Restore previous behaviour
		var onGetBlockAsyncFunc = node.Rpc.OnGetBlockAsync;
		node.Rpc.OnGetBlockAsync = _ => throw new HttpRequestException("some error");

		// Give time to hit errors
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Make GetBlockHash rpc calls to fail
		node.Rpc.OnGetBlockAsync = onGetBlockAsyncFunc; // Restore previous behaviour
		var onGetBlockHashAsyncFunc = node.Rpc.OnGetBlockHashAsync;
		node.Rpc.OnGetBlockHashAsync = _ => throw new TimeoutException();

		// Give time to hit errors
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		// Stop failing
		node.Rpc.OnGetBlockHashAsync = onGetBlockHashAsyncFunc;

		// Service should still be running
		Assert.Null(indexingStartTask.Exception);

		// Generate a new block
		var latestBlockHashes = await node.GenerateBlockAsync(CancellationToken.None);
		var latestBlockHash = latestBlockHashes[0];
		await Task.Delay(TimeSpan.FromSeconds(0.5));

		var bestBlockHash = await node.Rpc.GetBestBlockHashAsync();
		Assert.Equal(bestBlockHash, latestBlockHash);

		var lastFilter = await indexer.GetLastFilterAsync(CancellationToken.None);
		Assert.NotNull(lastFilter);
		Assert.Equal((uint)3, lastFilter.Header.Height);
		Assert.Equal(bestBlockHash, lastFilter.Header.BlockHash);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task DisposalCleansUpResourcesAsync()
	{
		var node = await MockNode.CreateNodeAsync(new MockNodeOptions(BlockToGenerate: 1));
		using var indexer = new IndexBuilderService(node.Rpc, _filtersPath, options: _options);

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

	[Fact]
	public async Task ConcurrentAccessIsThreadSafeAsync()
	{
		// Setup mock with 5 blocks
		var node = await MockNode.CreateNodeAsync(new MockNodeOptions(BlockToGenerate: 500));

		// Track how many times the blockchain info is requested
		int blockchainInfoRequestCount = 0;

		// Add delay to RPC calls to increase chance of concurrency issues
		var random = new Random(1234);

		var onGetBlockchainInfoAsyncFunc = node.Rpc.OnGetBlockchainInfoAsync!;
		var onGetBlockHashAsyncFunc = node.Rpc.OnGetBlockHashAsync!;
		var onGetVerboseBlockAsyncFunc = node.Rpc.OnGetVerboseBlockAsync!;

		node.Rpc.OnGetBlockchainInfoAsync = async () =>
		{
			blockchainInfoRequestCount++;
			// Random delay to simulate network latency
			await Task.Delay(random.Next(5, 20));
			return await onGetBlockchainInfoAsyncFunc();
		};
		node.Rpc.OnGetBlockHashAsync = async height =>
		{
			// Random delay to simulate network latency
			await Task.Delay(random.Next(5, 20));
			return await onGetBlockHashAsyncFunc(height);
		};
		node.Rpc.OnGetVerboseBlockAsync = async hash =>
		{
			// Random delay to simulate network latency
			await Task.Delay(random.Next(5, 20));
			return await onGetVerboseBlockAsyncFunc(hash);
		};

		// Create the indexer service
		using var indexer = new IndexBuilderService(node.Rpc, _filtersPath, options: _options);

		// Start the indexer
		var indexingTask = indexer.StartAsync(CancellationToken.None);

		// Prepare concurrent access tasks
		var concurrentTasks = new List<Task>();
		var exceptions = new ConcurrentQueue<Exception>();
		var readResults = new ConcurrentBag<(uint256 hash, uint height)>();

		// Track number of successful read/write operations
		int successfulReads = 0;
		int successfulWrites = 0;

		// Create a CancellationTokenSource that will stop all tasks after a timeout
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

		// Add concurrent reader tasks
		for (uint i = 0; i < 5; i++)
		{
			concurrentTasks.Add(Task.Run(async () =>
			{
				try
				{
					while (!cts.Token.IsCancellationRequested)
					{
						// Random delay between reads
						await Task.Delay(random.Next(10, 100), cts.Token);

						// Get last filter
						var lastFilter = await indexer.GetLastFilterAsync(CancellationToken.None);
						if (lastFilter != null)
						{
							readResults.Add((lastFilter.Header.BlockHash, lastFilter.Header.Height));
							Interlocked.Increment(ref successfulReads);
						}

						// Get some filters from an older point
						if (random.Next(5) == 0 && lastFilter != null && lastFilter.Header.Height > 2)
						{
							var height = random.Next(1, (int) lastFilter.Header.Height - 1);
							var startingBlockHash = await node.Rpc.GetBlockHashAsync(height);
							var filterLines = await indexer.GetFilterLinesExcludingAsync(startingBlockHash, 3, cts.Token);

							// Verify the returned data is consistent
							if (filterLines.found && filterLines.filters.Any())
							{
								// Check continuity of heights
								var heights = filterLines.filters.Select(f => f.Header.Height).ToList();
								for (int j = 1; j < heights.Count; j++)
								{
									if (heights[j] != heights[j - 1] + 1)
									{
										throw new Exception($"Non-continuous heights found: {heights[j - 1]} and {heights[j]}");
									}
								}

								// Check that the last filter's prev hash matches the previous filter's hash
								for (int j = 1; j < filterLines.filters.Count(); j++)
								{
									var prevFilter = filterLines.filters.ElementAt(j - 1);
									var currentFilter = filterLines.filters.ElementAt(j);

									if (currentFilter.Header.HeaderOrPrevBlockHash != prevFilter.Header.BlockHash)
									{
										throw new Exception($"Chain broken between heights {prevFilter.Header.Height} and {currentFilter.Header.Height}");
									}
								}

								Interlocked.Increment(ref successfulReads);
							}
						}
					}
				}
				catch (OperationCanceledException)
				{
					// Expected when cancellation is requested
				}
				catch (Exception ex)
				{
					exceptions.Enqueue(ex);
				}
			}, cts.Token));
		}

		// Add a task that simulates new blocks arriving
		concurrentTasks.Add(Task.Run(async () =>
		{
			try
			{
				// Start with 5 blocks and add more over time
				uint currentHeight = 5;

				while (!cts.Token.IsCancellationRequested && currentHeight < 100)
				{
					// Delay between adding new blocks
					await Task.Delay(random.Next(100, 500), cts.Token);

					// Increment the blockchain height
					await node.GenerateBlockAsync(cts.Token);

					Interlocked.Increment(ref successfulWrites);
				}
			}
			catch (OperationCanceledException)
			{
				// Expected when cancellation is requested
			}
			catch (Exception ex)
			{
				exceptions.Enqueue(ex);
			}
		}, cts.Token));

		// Add a task that occasionally checks the consistency
		concurrentTasks.Add(Task.Run(async () =>
		{
			try
			{
				while (!cts.Token.IsCancellationRequested)
				{
					// Check consistency every so often
					await Task.Delay(random.Next(300, 600), cts.Token);

					var lastFilter = await indexer.GetLastFilterAsync(CancellationToken.None);
					if (lastFilter != null && lastFilter.Header.Height > 1)
					{
						// Use GetFilterLinesExcluding to get the entire chain
						var allFilters = await indexer.GetFilterLinesExcludingAsync( lastFilter.Header.HeaderOrPrevBlockHash, 20, cts.Token);

						if (allFilters.found)
						{
							// Get the highest height filter
							var highestFilter = allFilters.filters.Last();

							// Make sure it matches what GetLastFilter returned
							if (highestFilter.Header.Height < lastFilter.Header.Height)
							{
								throw new Exception($"Inconsistency: GetLastFilter returned height {lastFilter.Header.Height} but GetFilterLinesExcluding highest filter is {highestFilter.Header.Height}");
							}

							// Check continuity throughout the chain
							var filters = allFilters.filters.ToList();
							for (int i = 1; i < filters.Count; i++)
							{
								if (filters[i].Header.Height != filters[i - 1].Header.Height + 1)
								{
									throw new Exception($"Height gap in filter chain between {filters[i - 1].Header.Height} and {filters[i].Header.Height}");
								}

								if (filters[i].Header.HeaderOrPrevBlockHash != filters[i - 1].Header.BlockHash)
								{
									throw new Exception($"Hash chain broken between {filters[i - 1].Header.Height} and {filters[i].Header.Height}");
								}
							}

							Interlocked.Increment(ref successfulReads);
						}
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Expected when cancellation is requested
			}
			catch (Exception ex)
			{
				exceptions.Enqueue(ex);
			}
		}, cts.Token));

		// Wait for all tasks to complete
		try
		{
			await Task.WhenAll(concurrentTasks);
		}
		catch (OperationCanceledException)
		{
			// Expected due to cancellation
		}

		// Stop the indexer
		await indexer.StopAsync(CancellationToken.None);
		await indexingTask;

		// Output statistics
		_testOutputHelper.WriteLine($"Blockchain info requested: {blockchainInfoRequestCount} times");
		_testOutputHelper.WriteLine($"Successful reads: {successfulReads}");
		_testOutputHelper.WriteLine($"Successful writes (block height increases): {successfulWrites}");
		_testOutputHelper.WriteLine($"Distinct block heights seen: {readResults.Select(r => r.height).Distinct().Count()}");

		// Assert that concurrent access didn't cause exceptions
		Assert.Empty(exceptions);

		// Assert we had substantial concurrent activity
		Assert.True(successfulReads >= 10, "Should have performed at least 10 successful reads");
		Assert.True(blockchainInfoRequestCount >= 5, "Should have made at least 5 blockchain info requests");

		// Verify filter chain integrity
		var finalFilter = await indexer.GetLastFilterAsync(CancellationToken.None);
		Assert.NotNull(finalFilter);

		// Use service API to get all filters
		var allFilters = await indexer.GetFilterLinesExcludingAsync(
			StartingFilters.GetStartingFilter(node.Network).Header.BlockHash,
			100,
			CancellationToken.None);

		// Verify chain integrity
		var orderedFilters = allFilters.filters.OrderBy(f => f.Header.Height).ToList();
		for (int i = 1; i < orderedFilters.Count; i++)
		{
			// Each filter should have a height exactly one more than the previous
			Assert.Equal(orderedFilters[i - 1].Header.Height + 1, orderedFilters[i].Header.Height);

			// Each filter's prev hash should match the previous filter's hash
			Assert.Equal(orderedFilters[i - 1].Header.BlockHash, orderedFilters[i].Header.HeaderOrPrevBlockHash);
		}
	}
}
