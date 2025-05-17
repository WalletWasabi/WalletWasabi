using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.BlockProvider;
using WalletWasabi.Wallets.FilterProcessor;
using Xunit;
using static WalletWasabi.Wallets.FilterProcessor.BlockDownloadService;

namespace WalletWasabi.Tests.UnitTests.Wallet.FilterProcessor;

/// <summary>
/// Tests for <see cref="BlockDownloadService"/>.
/// </summary>
/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class BlockDownloadServiceTests
{
	/// <summary>
	/// Tests <see cref="BlockDownloadService.TryGetBlockAsync(Source, uint256, Priority, uint, CancellationToken)"/> method.
	/// Blocks should be downloaded in parallel. Especially, if we get one block request and then immediately another one, BDS is supposed
	/// to start both tasks and not wait for the first one to finish. Meaning, BDS is not supposed to work in batches until it hits its
	/// level of parallelism (<see cref="BlockDownloadService.MaximumParallelTasks"/>).
	/// </summary>
	[Fact]
	public async Task TryGetBlockTests1Async()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		uint256 blockHash1 = uint256.One;
		uint256 blockHash2 = new(2);
		uint256 blockHash3 = new(3);
		uint256 blockHash4 = new(4);

		Block block1 = Network.Main.Consensus.ConsensusFactory.CreateBlock();
		Block block2 = Network.Main.Consensus.ConsensusFactory.CreateBlock();
		Block block3 = Network.Main.Consensus.ConsensusFactory.CreateBlock();
		Block block4 = Network.Main.Consensus.ConsensusFactory.CreateBlock();

		TaskCompletionSource block1RequestedTcs = new();
		TaskCompletionSource block1DelayTcs = new();

		TaskCompletionSource block2RequestedTcs = new();
		TaskCompletionSource block2DelayTcs = new();

		var fileSystemBlockRepository = new TesteableFileSystemBlockRepository
		{
			OnTryGetBlockAsync = (_, _) => Task.FromResult<Block?>(null),
			OnSaveAsync = (_, _) => Task.CompletedTask
		};

		int block2Counter = 0;
		IBlockProvider fullNodeBlockProvider = new TesteableBlockProvider
		{
			OnTryGetBlockAsync = async (blockHash, _) =>
			{
				if (blockHash == blockHash1)
				{
					block1RequestedTcs.SetResult();

					// Wait until signal is given that "block is downloaded".
					await block1DelayTcs.Task.WaitAsync(testCts.Token).ConfigureAwait(false);

					return block1;
				}
				if(blockHash == blockHash2)
				{
					block2Counter++;
					if (block2Counter == 1)
					{
						block2RequestedTcs.SetResult();

						// Wait until signal is given that a download failure can be reported here.
						await block2DelayTcs.Task.WaitAsync(testCts.Token).ConfigureAwait(false);

						return null;
					}
					return block2;
				}

				if (blockHash == blockHash3)
				{
					return block3;
				}
				if (blockHash == blockHash4)
				{
					return block4;
				}

				throw new Exception("WTF");
			}
		};

		using (BlockDownloadService service = new(fileSystemBlockRepository, [fullNodeBlockProvider], p2pBlockProvider: null, maximumParallelTasks: 3))
		{
			await service.StartAsync(testCts.Token);

			var task1 = service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash1, 611_001, testCts.Token);
			var task2 = service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash2, 610_002, testCts.Token);
			var task3 = service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash3, 610_003, testCts.Token);
			var task4 = service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash4, 610_004, testCts.Token);

			// Downloading of the block1 waits for our signal.
			{
				Assert.False(task1.IsCompletedSuccessfully);
				await block1RequestedTcs.Task.WaitAsync(testCts.Token);
			}

			// Add small delay to make sure that things stabilize.
			await Task.Delay(100, testCts.Token);

			// Allow downloading of the block1.
			{
				Assert.False(task1.IsCompletedSuccessfully);
				block1DelayTcs.SetResult();

				// Block2 should be available even though block1 and block3 are waiting for data.
				var task1Result = await task1;
				Assert.True(task1Result.IsOk);
				Assert.Same(block1, task1Result.Value);
			}

			// Verify that block2 waits for our signal, unblock it and verify result.
			{
				Assert.False(task2.IsCompletedSuccessfully);
				await block2RequestedTcs.Task.WaitAsync(testCts.Token);
				block2DelayTcs.SetResult();

				var task2Result = await task2;

				Assert.Equal(DownloadError.Failure, task2Result.Error);
			}

			// All tasks should provide data now.
			Task<Result<Block, DownloadError>>[] tasks = [task1, task2, task3, task4];
			await Task.WhenAll(tasks);

			// Second attempt to download block2 should succeed.
			{
				var task2Result = await service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash2, blockHeight: 610_002, testCts.Token);
				Assert.True(task2Result.IsOk);
				Assert.Same(block2, task2Result.Value);
			}

			await service.StopAsync(testCts.Token);
			await service.ExecuteTask!.WaitAsync(testCts.Token);
		}

		Block? actualBlock2 = await fullNodeBlockProvider.TryGetBlockAsync(blockHash2, testCts.Token);
		Assert.Same(block2, actualBlock2);
	}

	/// <summary>
	/// Tests <see cref="BlockDownloadService.TryGetBlockAsync(uint256, Priority, uint, CancellationToken)"/>.
	/// </summary>
	[Fact]
	public async Task TryGetBlockTests2Async()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		uint256 blockHash1 = uint256.One;
		uint256 blockHash2 = new(2);
		uint256 blockHash3 = new(3);
		uint256 blockHash4 = new(4);

		Block block1 = Network.Main.Consensus.ConsensusFactory.CreateBlock();
		Block block2 = Network.Main.Consensus.ConsensusFactory.CreateBlock();
		Block block3 = Network.Main.Consensus.ConsensusFactory.CreateBlock();
		Block block4 = Network.Main.Consensus.ConsensusFactory.CreateBlock();

		// File-system cache is disabled for the purposes of this test.
		var fileSystemBlockRepository = new TesteableFileSystemBlockRepository
		{
			OnTryGetBlockAsync = (_, _) => Task.FromResult<Block?>(null),
			OnSaveAsync = (_, _) => Task.CompletedTask
		};

		int block2Counter=0;
		IBlockProvider fullNodeBlockProvider = new TesteableBlockProvider
		{
			OnTryGetBlockAsync = (blockHash, _) =>
			{
				var blk = (blockHash, block2Counter) switch
				{
					(_, _) when blockHash == blockHash1 => block1,
					(_, 0) when blockHash == blockHash2 => null,
					(_, _) when blockHash == blockHash2 => block2,
					(_, _) when blockHash == blockHash3 => block3,
					(_, _) when blockHash == blockHash4 => block4,
				};
				if (blockHash == blockHash2) { block2Counter++;}
				return Task.FromResult(blk);
			}
		};

		using (BlockDownloadService service = new(fileSystemBlockRepository, [fullNodeBlockProvider], p2pBlockProvider: null, maximumParallelTasks: 3))
		{
			await service.StartAsync(testCts.Token);

			var actualResult1 = await service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash1,  blockHeight: 610_001, testCts.Token);
			Assert.True(actualResult1.IsOk);
			Assert.Same(block1, actualResult1.Value);

			var actualResult2 = await service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash2,  blockHeight: 610_002, testCts.Token);
			Assert.False(actualResult2.IsOk);

			var actualResult3 = await service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash3,  blockHeight: 610_003, testCts.Token);
			Assert.True(actualResult3.IsOk);
			Assert.Same(block3, actualResult3.Value);

			var actualResult4 = await service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash4,  blockHeight: 610_004, testCts.Token);
			Assert.True(actualResult4.IsOk);
			Assert.Same(block4, actualResult4.Value);

			// Second attempt to get block2.
			actualResult2 = await service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash2, blockHeight: 610_002, testCts.Token);
			Assert.True(actualResult2.IsOk);
			Assert.Same(block2, actualResult2.Value);

			// Getting a block over P2P fails because there is no P2P provider registered.
			var actualResult5 = await service.TryGetBlockAsync(BlockSource.P2pNetwork, blockHash2, blockHeight: 610_005, testCts.Token);
			Assert.False(actualResult5.IsOk);
			Assert.Equal(DownloadError.NoSuchProvider, actualResult5.Error);

			await service.StopAsync(testCts.Token);
			await service.ExecuteTask!.WaitAsync(testCts.Token);
		}
	}

	[Fact]
	public async Task RemoveBlocksAsync()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		uint256 blockHash1 = uint256.One;
		uint256 blockHash2 = new(2);
		uint256 blockHash3 = new(3);
		uint256 blockHash4 = new(4);

		var fileSystemBlockRepository = new TesteableFileSystemBlockRepository
		{
			OnTryGetBlockAsync = (_, _) => Task.FromResult<Block?>(null),
			OnSaveAsync = (_, _) => Task.CompletedTask,
			OnRemoveAsync = (_,_) =>Task.CompletedTask,
		};

		IBlockProvider fullNodeBlockProvider = new TesteableBlockProvider
		{
			OnTryGetBlockAsync = (_, _) => Task.FromResult((Block?)null)
		};

		using BlockDownloadService service = new(fileSystemBlockRepository, [fullNodeBlockProvider], p2pBlockProvider: null, maximumParallelTasks: 3);

		// Intentionally, tested before the service is started just to smoke test that the queue is modified.
		Task<Result<Block, DownloadError>>[] tasks = [
			service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash1, 610_001, testCts.Token),
			service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash2, 610_002, testCts.Token),
			service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash3, 610_003, testCts.Token),
			service.TryGetBlockAsync(BlockSource.TrustedNode, blockHash4, 610_004, testCts.Token)];

		// Remove blocks >= 610_003.
		await service.RemoveBlocksAsync(maxBlockHeight: 610_003);

		Request[] actualRequests = service.BlocksToDownloadRequests.UnorderedItems
			.Select(x => x.Element)
			.OrderBy(x => x.BlockHeight)
			.ToArray();

		// Block 610_004 should be removed.
		Assert.Equal(3, actualRequests.Length);
		Assert.Equal(610_001u, actualRequests[0].BlockHeight);
		Assert.Equal(610_002u, actualRequests[1].BlockHeight);
		Assert.Equal(610_003u, actualRequests[2].BlockHeight);

		// Start the service late.
		await service.StartAsync(testCts.Token);

		await Task.WhenAll(tasks);

		foreach (var blockDownloadTask in tasks.SkipLast(1))
		{
			var result = await blockDownloadTask;
			Assert.False(result.IsOk);
			Assert.Equal(DownloadError.Failure, result.Error);
		}

		foreach (var blockDownloadTask in tasks.Skip(3))
		{
			var result = await blockDownloadTask;
			Assert.False(result.IsOk);
			Assert.Equal(DownloadError.ReorgOccurred, result.Error);
		}

		await service.StopAsync(testCts.Token);
	}
}
