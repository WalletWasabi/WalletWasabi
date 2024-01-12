using Moq;
using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.FilterProcessor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet.FilterProcessor;

/// <summary>
/// Tests for <see cref="BlockDownloadService"/>.
/// </summary>
public class BlockDownloadServiceTests
{
	/// <summary>
	/// Verifies that blocks are being downloaded in parallel. Moreover, we attempt to download a block again if it fails to download.
	/// </summary>
	[Fact]
	public async Task BlockDownloadingTestAsync()
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

		Mock <IBlockProvider> mockBlockProvider = new(MockBehavior.Strict);
		IBlockProvider blockProvider = mockBlockProvider.Object;

		using (BlockDownloadService service = new(mockFileSystemBlockRepository.Object, blockProvider, maximumParallelTasks: 3))
		{
			// Handling of downloading of block1.
			_ = mockBlockProvider.Setup(c => c.TryGetBlockAsync(blockHash1, It.IsAny<CancellationToken>()))
				.ReturnsAsync(block1);

			// Handling of downloading of block2.
			_ = mockBlockProvider.SetupSequence(c => c.TryGetBlockAsync(blockHash2, It.IsAny<CancellationToken>()))
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
			_ = mockBlockProvider.Setup(c => c.TryGetBlockAsync(blockHash3, It.IsAny<CancellationToken>()))
				.ReturnsAsync(block3);

			// Handling of downloading of block4.
			_ = mockBlockProvider.Setup(c => c.TryGetBlockAsync(blockHash4, It.IsAny<CancellationToken>()))
				.ReturnsAsync(block4);

			await service.StartAsync(testCts.Token);
			service.Enqueue(blockHash1, new Priority(SyncType.Complete, BlockHeight: 610_001));
			service.Enqueue(blockHash2, new Priority(SyncType.Complete, BlockHeight: 610_002));
			service.Enqueue(blockHash3, new Priority(SyncType.Complete, BlockHeight: 610_003));
			service.Enqueue(blockHash4, new Priority(SyncType.Complete, BlockHeight: 610_004));

			await block2FirstRequestTcs.Task.WaitAsync(testCts.Token);
			block2DelayTcs.SetResult();
			await block2DownloadedTcs.Task.WaitAsync(testCts.Token);

			await service.StopAsync(testCts.Token);
			await service.ExecuteTask!.WaitAsync(testCts.Token);
		}

		Block? actualBlock2 = await blockProvider.TryGetBlockAsync(blockHash2, testCts.Token);
		Assert.Same(block2, actualBlock2);

		mockBlockProvider.VerifyAll();
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

		Mock<IBlockProvider> mockBlockProvider = new(MockBehavior.Strict);
		IBlockProvider blockProvider = mockBlockProvider.Object;

		int actualAttempts = 0;
		bool testFailed = false;

		using (BlockDownloadService service = new(mockFileSystemBlockRepository.Object,blockProvider, maximumParallelTasks: 3))
		{
			// Handling of downloading of block1.
			_ = mockBlockProvider.Setup(c => c.TryGetBlockAsync(blockHash1, It.IsAny<CancellationToken>()))
				.ReturnsAsync((uint256 blockHash, CancellationToken cancellationToken) =>
				{
					actualAttempts++;

					switch (actualAttempts)
					{
						case < BlockDownloadService.MaxFailedAttempts:
							break;
						case BlockDownloadService.MaxFailedAttempts:
							block1LastFailedAttemptTcs.SetResult();
							break;
						case > BlockDownloadService.MaxFailedAttempts:
							testFailed = true; // This should never happen.
							break;
					}

					return null;
				});

			await service.StartAsync(testCts.Token);

			service.Enqueue(blockHash1, new Priority(SyncType.Complete, BlockHeight: 610_001));

			// Wait for all failed attempts.
			await block1LastFailedAttemptTcs.Task.WaitAsync(testCts.Token);

			await service.StopAsync(testCts.Token);
			await service.ExecuteTask!.WaitAsync(testCts.Token);

			// Make sure that the block is really dropped.
			Assert.Equal(0, service.BlocksToDownload.Count);
		}

		Assert.False(testFailed);

		mockBlockProvider.VerifyAll();
	}

	[Fact]
	public void RemoveBlocks()
	{
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

		Mock<IBlockProvider> mockBlockProvider = new(MockBehavior.Strict);
		IBlockProvider blockProvider = mockBlockProvider.Object;

		using BlockDownloadService service = new(mockFileSystemBlockRepository.Object, blockProvider, maximumParallelTasks: 3);

		// Intentionally, tested before the service is started just to smoke test that the queue is modified.
		service.Enqueue(blockHash1, new Priority(SyncType.Complete, 610_001));
		service.Enqueue(blockHash2, new Priority(SyncType.Complete, 610_002));
		service.Enqueue(blockHash3, new Priority(SyncType.Complete, 610_003));
		service.Enqueue(blockHash4, new Priority(SyncType.Complete, 610_004));

		// Remove blocks >= 610_003.
		service.RemoveBlocks(maxBlockHeight: 610_003);

		BlockDownloadService.Request[] actualRequests = service.BlocksToDownload.UnorderedItems
			.Select(x => x.Element)
			.OrderBy(x => x.Priority.BlockHeight)
			.ToArray();

		// Block 610_004 should be removed.
		Assert.Equal(3, actualRequests.Length);
		Assert.Equal(610_001u, actualRequests[0].Priority.BlockHeight);
		Assert.Equal(610_002u, actualRequests[1].Priority.BlockHeight);
		Assert.Equal(610_003u, actualRequests[2].Priority.BlockHeight);
	}

	// TODO: Test for "block provider being cancelled when fetching a block".
}
