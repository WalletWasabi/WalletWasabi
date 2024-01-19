using Moq;
using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.BlockProvider;
using WalletWasabi.Wallets.FilterProcessor;
using Xunit;
using static WalletWasabi.Wallets.FilterProcessor.BlockDownloadService;

namespace WalletWasabi.Tests.UnitTests.Wallet.FilterProcessor;

/// <summary>
/// Tests for <see cref="BlockDownloadService"/>.
/// </summary>
public class BlockDownloadServiceTests
{
	private const uint MaxAttempts = 3;

	/// <summary>
	/// Tests <see cref="BlockDownloadService.TryGetBlockAsync(Source, uint256, Priority, uint, CancellationToken)"/> method. Blocks should be downloaded in parallel.
	/// Moreover, we attempt to download a block again if it fails to download.
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

		TaskCompletionSource block2FirstRequestTcs = new();
		TaskCompletionSource block2DelayTcs = new();
		TaskCompletionSource block2DownloadedTcs = new();

		Mock<IFileSystemBlockRepository> mockFileSystemBlockRepository = new(MockBehavior.Strict);
		_ = mockFileSystemBlockRepository.Setup(c => c.TryGetAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((Block?)null);

		_ = mockFileSystemBlockRepository.Setup(c => c.SaveAsync(It.IsAny<Block>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		Mock<IBlockProvider> mockFullNodeBlockProvider = new(MockBehavior.Strict);
		IBlockProvider fullNodeBlockProvider = mockFullNodeBlockProvider.Object;

		using (BlockDownloadService service = new(mockFileSystemBlockRepository.Object, [fullNodeBlockProvider], p2pBlockProvider: null, maximumParallelTasks: 3))
		{
			// Handling of downloading of block1.
			_ = mockFullNodeBlockProvider.Setup(c => c.TryGetBlockAsync(blockHash1, It.IsAny<CancellationToken>()))
				.ReturnsAsync(block1);

			// Handling of downloading of block2.
			_ = mockFullNodeBlockProvider.SetupSequence(c => c.TryGetBlockAsync(blockHash2, It.IsAny<CancellationToken>()))
				.Returns(async () =>
				{
					block2FirstRequestTcs.SetResult();

					// Wait until signal is given that a download failure can be reporter here.
					await block2DelayTcs.Task.WaitAsync(testCts.Token).ConfigureAwait(false);
					return null;
				})
				.ReturnsAsync(() =>
				{
					block2DownloadedTcs.SetResult();
					return block2;
				}) // Called by the service.
				.ReturnsAsync(block2); // Called by the test to verify we got here.

			// Handling of downloading of block3.
			_ = mockFullNodeBlockProvider.Setup(c => c.TryGetBlockAsync(blockHash3, It.IsAny<CancellationToken>()))
				.ReturnsAsync(block3);

			// Handling of downloading of block4.
			_ = mockFullNodeBlockProvider.Setup(c => c.TryGetBlockAsync(blockHash4, It.IsAny<CancellationToken>()))
				.ReturnsAsync(block4);

			await service.StartAsync(testCts.Token);

			Task<IResult>[] tasks = [
				service.TryGetBlockAsync(Source.TrustedFullNode, blockHash1, new Priority(SyncType.Complete, BlockHeight: 610_001), MaxAttempts, testCts.Token),
				service.TryGetBlockAsync(Source.TrustedFullNode, blockHash2, new Priority(SyncType.Complete, BlockHeight: 610_002), MaxAttempts, testCts.Token),
				service.TryGetBlockAsync(Source.TrustedFullNode, blockHash3, new Priority(SyncType.Complete, BlockHeight: 610_003), MaxAttempts, testCts.Token),
				service.TryGetBlockAsync(Source.TrustedFullNode, blockHash4, new Priority(SyncType.Complete, BlockHeight: 610_004), MaxAttempts, testCts.Token),
			];

			await block2FirstRequestTcs.Task.WaitAsync(testCts.Token);
			block2DelayTcs.SetResult();
			await block2DownloadedTcs.Task.WaitAsync(testCts.Token);

			Task.WaitAll(tasks);

			await service.StopAsync(testCts.Token);
			await service.ExecuteTask!.WaitAsync(testCts.Token);
		}

		Block? actualBlock2 = await fullNodeBlockProvider.TryGetBlockAsync(blockHash2, testCts.Token);
		Assert.Same(block2, actualBlock2);

		mockFullNodeBlockProvider.VerifyAll();
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

		Mock<IFileSystemBlockRepository> mockFileSystemBlockRepository = new(MockBehavior.Strict);
		_ = mockFileSystemBlockRepository.Setup(c => c.TryGetAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((Block?)null);

		_ = mockFileSystemBlockRepository.Setup(c => c.SaveAsync(It.IsAny<Block>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		Mock<IBlockProvider> mockFullNodeBlockProvider = new(MockBehavior.Strict);
		IBlockProvider fullNodeBlockProvider = mockFullNodeBlockProvider.Object;

		using (BlockDownloadService service = new(mockFileSystemBlockRepository.Object, [fullNodeBlockProvider], p2pBlockProvider: null, maximumParallelTasks: 3))
		{
			// Handling of downloading of block1.
			_ = mockFullNodeBlockProvider.Setup(c => c.TryGetBlockAsync(blockHash1, It.IsAny<CancellationToken>()))
				.ReturnsAsync(block1);

			// Handling of downloading of block2.
			_ = mockFullNodeBlockProvider.SetupSequence(c => c.TryGetBlockAsync(blockHash2, It.IsAny<CancellationToken>()))
				.ReturnsAsync((Block?)null)
				.ReturnsAsync(block2) // Called by the service.
				.ReturnsAsync(block2); // Called by the test to verify we got here.

			// Handling of downloading of block3.
			_ = mockFullNodeBlockProvider.Setup(c => c.TryGetBlockAsync(blockHash3, It.IsAny<CancellationToken>()))
				.ReturnsAsync(block3);

			// Handling of downloading of block4.
			_ = mockFullNodeBlockProvider.Setup(c => c.TryGetBlockAsync(blockHash4, It.IsAny<CancellationToken>()))
				.ReturnsAsync(block4);

			await service.StartAsync(testCts.Token);

			IResult actualResult1 = await service.TryGetBlockAsync(Source.TrustedFullNode, blockHash1, new Priority(SyncType.Complete, BlockHeight: 610_001), MaxAttempts, testCts.Token);
			SuccessResult actualSuccessResult1 = Assert.IsType<SuccessResult>(actualResult1);
			Assert.Same(block1, actualSuccessResult1.Block);

			IResult actualResult2 = await service.TryGetBlockAsync(Source.TrustedFullNode, blockHash2, new Priority(SyncType.Complete, BlockHeight: 610_002), MaxAttempts, testCts.Token);
			SuccessResult actualSuccessResult2 = Assert.IsType<SuccessResult>(actualResult2);
			Assert.Same(block2, actualSuccessResult2.Block);

			IResult actualResult3 = await service.TryGetBlockAsync(Source.TrustedFullNode, blockHash3, new Priority(SyncType.Complete, BlockHeight: 610_003), MaxAttempts, testCts.Token);
			SuccessResult actualSuccessResult3 = Assert.IsType<SuccessResult>(actualResult3);
			Assert.Same(block3, actualSuccessResult3.Block);

			IResult actualResult4 = await service.TryGetBlockAsync(Source.TrustedFullNode, blockHash4, new Priority(SyncType.Complete, BlockHeight: 610_004), MaxAttempts, testCts.Token);
			SuccessResult actualSuccessResult4 = Assert.IsType<SuccessResult>(actualResult4);
			Assert.Same(block4, actualSuccessResult4.Block);

			await service.StopAsync(testCts.Token);
			await service.ExecuteTask!.WaitAsync(testCts.Token);
		}

		mockFullNodeBlockProvider.VerifyAll();
	}

	/// <summary>
	/// Verifies that a block is attempted to be downloaded at most <see cref="BlockDownloadService.MaxFailedAttempts"/> times.
	/// </summary>
	[Fact]
	public async Task MaxBlockDownloadAttemptsAsync()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		uint256 blockHash1 = uint256.One;
		Block block1 = Network.Main.Consensus.ConsensusFactory.CreateBlock();

		TaskCompletionSource block1LastFailedAttemptTcs = new();

		Mock<IFileSystemBlockRepository> mockFileSystemBlockRepository = new(MockBehavior.Strict);
		_ = mockFileSystemBlockRepository.Setup(c => c.TryGetAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((Block?)null);

		Mock<IBlockProvider> mockFullNodeBlockProvider = new(MockBehavior.Strict);
		IBlockProvider fullNodeBlockProvider = mockFullNodeBlockProvider.Object;

		uint actualAttempts = 0;
		bool testFailed = false;

		using (BlockDownloadService service = new(mockFileSystemBlockRepository.Object, [fullNodeBlockProvider], p2pBlockProvider: null, maximumParallelTasks: 3))
		{
			// Handling of downloading of block1.
			_ = mockFullNodeBlockProvider.Setup(c => c.TryGetBlockAsync(blockHash1, It.IsAny<CancellationToken>()))
				.ReturnsAsync((uint256 blockHash, CancellationToken cancellationToken) =>
				{
					actualAttempts++;

					switch (actualAttempts)
					{
						case < MaxAttempts:
							break;
						case MaxAttempts:
							block1LastFailedAttemptTcs.SetResult();
							return block1;
						case > MaxAttempts:
							testFailed = true; // This should never happen.
							break;
					}

					return null;
				});

			await service.StartAsync(testCts.Token);

			Task<IResult> block1Task = service.TryGetBlockAsync(Source.TrustedFullNode, blockHash1, new Priority(SyncType.Complete, BlockHeight: 610_001), maxAttempts: MaxAttempts, testCts.Token);

			// Wait for all failed attempts.
			await block1LastFailedAttemptTcs.Task.WaitAsync(testCts.Token);

			IResult block1Result = await block1Task;
			SuccessResult actualSuccessResult = Assert.IsType<SuccessResult>(block1Result);
			Assert.Same(block1, actualSuccessResult.Block);

			await service.StopAsync(testCts.Token);
			await service.ExecuteTask!.WaitAsync(testCts.Token);

			// Make sure that the block is really dropped.
			Assert.Equal(0, service.BlocksToDownload.Count);
		}

		Assert.False(testFailed);

		mockFullNodeBlockProvider.VerifyAll();
	}

	[Fact]
	public async Task RemoveBlocksAsync()
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

		Mock<IFileSystemBlockRepository> mockFileSystemBlockRepository = new(MockBehavior.Strict);
		_ = mockFileSystemBlockRepository.Setup(c => c.TryGetAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((Block?)null);

		Mock<IBlockProvider> mockFullNodeBlockProvider = new(MockBehavior.Strict);
		IBlockProvider fullNodeBlockProvider = mockFullNodeBlockProvider.Object;
		_ = mockFullNodeBlockProvider.Setup(c => c.TryGetBlockAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((Block?)null);

		using BlockDownloadService service = new(mockFileSystemBlockRepository.Object, [fullNodeBlockProvider], p2pBlockProvider: null, maximumParallelTasks: 3);

		// Intentionally, tested before the service is started just to smoke test that the queue is modified.
		Task<IResult>[] tasks = [
			service.TryGetBlockAsync(Source.TrustedFullNode, blockHash1, new Priority(SyncType.Complete, 610_001), maxAttempts: 1, testCts.Token),
			service.TryGetBlockAsync(Source.TrustedFullNode, blockHash2, new Priority(SyncType.Complete, 610_002), maxAttempts: 1, testCts.Token),
			service.TryGetBlockAsync(Source.TrustedFullNode, blockHash3, new Priority(SyncType.Complete, 610_003), maxAttempts: 1, testCts.Token),
			service.TryGetBlockAsync(Source.TrustedFullNode, blockHash4, new Priority(SyncType.Complete, 610_004), maxAttempts: 1, testCts.Token),
		];

		// Remove blocks >= 610_003.
		service.RemoveBlocks(maxBlockHeight: 610_003);

		Request[] actualRequests = service.BlocksToDownload.UnorderedItems
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

		Task.WaitAll(tasks);

		foreach (Task<IResult> blockDownloadTask in tasks.SkipLast(1))
		{
			IResult result = await blockDownloadTask;
			FailureResult failureResult = Assert.IsType<FailureResult>(result);
			Assert.Equal(1u, failureResult.Attempts);
			Assert.IsType<EmptySourceData>(failureResult.SourceData);
		}

		foreach (Task<IResult> blockDownloadTask in tasks.Skip(3))
		{
			IResult result = await blockDownloadTask;
			ReorgOccurredResult reorgResult = Assert.IsType<ReorgOccurredResult>(result);
			Assert.Equal(610_003u, reorgResult.NewBlockchainHeight);
		}

		await service.StopAsync(testCts.Token);
	}
}
