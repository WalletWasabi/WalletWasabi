using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

			Task<IResult> task1 = service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash1, new Priority(BlockHeight: 610_001), testCts.Token);
			Task<IResult> task2 = service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash2, new Priority(BlockHeight: 610_002), testCts.Token);
			Task<IResult> task3 = service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash3, new Priority(BlockHeight: 610_003), testCts.Token);
			Task<IResult> task4 = service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash4, new Priority(BlockHeight: 610_004), testCts.Token);

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
				IResult task1Result = await task1;
				SuccessResult successResult = Assert.IsType<SuccessResult>(task1Result);
				Assert.Same(block1, successResult.Block);
			}

			// Verify that block2 waits for our signal, unblock it and verify result.
			{
				Assert.False(task2.IsCompletedSuccessfully);
				await block2RequestedTcs.Task.WaitAsync(testCts.Token);
				block2DelayTcs.SetResult();

				IResult task2Result = await task2;

				FailureResult failureResult = Assert.IsType<FailureResult>(task2Result);
			}

			// All tasks should provide data now.
			Task<IResult>[] tasks = [task1, task2, task3, task4];
			await Task.WhenAll(tasks);

			// Second attempt to download block2 should succeed.
			{
				IResult task2Result = await service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash2, new Priority(BlockHeight: 610_002), testCts.Token);
				SuccessResult successResult = Assert.IsType<SuccessResult>(task2Result);
				Assert.Same(block2, successResult.Block);
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

			IResult actualResult1 = await service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash1, new Priority(BlockHeight: 610_001), testCts.Token);
			SuccessResult actualSuccessResult1 = Assert.IsType<SuccessResult>(actualResult1);
			Assert.Same(block1, actualSuccessResult1.Block);

			IResult actualResult2 = await service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash2, new Priority(BlockHeight: 610_002), testCts.Token);
			FailureResult actualFailureResult2 = Assert.IsType<FailureResult>(actualResult2);

			IResult actualResult3 = await service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash3, new Priority(BlockHeight: 610_003), testCts.Token);
			SuccessResult actualSuccessResult3 = Assert.IsType<SuccessResult>(actualResult3);
			Assert.Same(block3, actualSuccessResult3.Block);

			IResult actualResult4 = await service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash4, new Priority(BlockHeight: 610_004), testCts.Token);
			SuccessResult actualSuccessResult4 = Assert.IsType<SuccessResult>(actualResult4);
			Assert.Same(block4, actualSuccessResult4.Block);

			// Second attempt to get block2.
			actualResult2 = await service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash2, new Priority(BlockHeight: 610_002), testCts.Token);
			SuccessResult actualSuccessResult2 = Assert.IsType<SuccessResult>(actualResult2);
			Assert.Same(block2, actualSuccessResult2.Block);

			// Getting a block over P2P fails because there is no P2P provider registered.
			IResult actualResult5 = await service.TryGetBlockAsync(P2pSourceRequest.Automatic, blockHash2, new Priority(BlockHeight: 610_005), testCts.Token);
			Assert.IsType<NoSuchProviderResult>(actualResult5);

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
		Task<IResult>[] tasks = [
			service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash1, new Priority(610_001), testCts.Token),
			service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash2, new Priority(610_002), testCts.Token),
			service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash3, new Priority(610_003), testCts.Token),
			service.TryGetBlockAsync(TrustedFullNodeSourceRequest.Instance, blockHash4, new Priority(610_004), testCts.Token)];

		// Remove blocks >= 610_003.
		await service.RemoveBlocksAsync(maxBlockHeight: 610_003);

		Request[] actualRequests = service.BlocksToDownloadRequests.UnorderedItems
			.Select(x => x.Element)
			.OrderBy(x => x.Priority.BlockHeight)
			.ToArray();

		// Block 610_004 should be removed.
		Assert.Equal(3, actualRequests.Length);
		Assert.Equal(610_001u, actualRequests[0].Priority.BlockHeight);
		Assert.Equal(610_002u, actualRequests[1].Priority.BlockHeight);
		Assert.Equal(610_003u, actualRequests[2].Priority.BlockHeight);

		// Start the service late.
		await service.StartAsync(testCts.Token);

		await Task.WhenAll(tasks);

		foreach (Task<IResult> blockDownloadTask in tasks.SkipLast(1))
		{
			IResult result = await blockDownloadTask;
			Assert.IsType<FailureResult>(result);
		}

		foreach (Task<IResult> blockDownloadTask in tasks.Skip(3))
		{
			IResult result = await blockDownloadTask;
			Assert.IsType<ReorgOccurredResult>(result);
		}

		await service.StopAsync(testCts.Token);
	}
}
