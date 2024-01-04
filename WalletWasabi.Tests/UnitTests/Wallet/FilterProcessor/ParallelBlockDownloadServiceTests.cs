using Moq;
using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.FilterProcessor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet.FilterProcessor;

/// <summary>
/// Tests for <see cref="ParallelBlockDownloadService"/>.
/// </summary>
public class ParallelBlockDownloadServiceTests
{
	/// <summary>
	/// Verifies that blocks are being downloaded in parallel. Moreover, we attempt to download a block again if it fails to download.
	/// </summary>
	[Fact]
	public async Task BlockDownloadingTestAsync()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(5));

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

		Mock<IBlockProvider> mockBlockProvider = new(MockBehavior.Strict);
		IBlockProvider blockProvider = mockBlockProvider.Object;

		using (ParallelBlockDownloadService service = new(blockProvider, maximumParallelTasks: 3))
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
			service.Enqueue(blockHash1, blockHeight: 610_001);
			service.Enqueue(blockHash2, blockHeight: 610_002);
			service.Enqueue(blockHash3, blockHeight: 610_003);
			service.Enqueue(blockHash4, blockHeight: 610_004);

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

	[Fact]
	public async Task RemoveBlocksAsync()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		// TODO.
	}
}
