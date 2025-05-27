using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet.FilterProcessor;

[Collection("Serial unit tests collection")]
public class BlockDownloadServiceTests
{
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
		BlockProvider fullNodeBlockProvider = async (blockHash, _ ) =>
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
		};

		var tryGetBlock = BlockProviders.CachedBlockProvider(fullNodeBlockProvider, fileSystemBlockRepository);

		var task1 = tryGetBlock(blockHash1, testCts.Token);
		var task2 = tryGetBlock(blockHash2, testCts.Token);
		var task3 = tryGetBlock(blockHash3, testCts.Token);
		var task4 = tryGetBlock(blockHash4, testCts.Token);

		// Downloading of the block1 waits for our signal.
		{
			Assert.False(task1.IsCompletedSuccessfully);
			await block1RequestedTcs.Task.WaitAsync(testCts.Token);
		}

		// Allow downloading of the block1.
		{
			Assert.False(task1.IsCompletedSuccessfully);
			block1DelayTcs.SetResult();

			// Block2 should be available even though block1 and block3 are waiting for data.
			var block = await task1;
			Assert.Same(block1, block);
		}

		// Verify that block2 waits for our signal, unblock it and verify result.
		{
			Assert.False(task2.IsCompletedSuccessfully);
			await block2RequestedTcs.Task.WaitAsync(testCts.Token);
			block2DelayTcs.SetResult();

			var block = await task2;
			Assert.Null(block);
		}

		// All tasks should provide data now.
		Task<Block?>[] tasks = [task1, task2, task3, task4];
		await Task.WhenAll(tasks);

		// Second attempt to download block2 should succeed.
		{
			var block = await tryGetBlock(blockHash2, testCts.Token);
			Assert.Same(block2, block);
		}

		Block? actualBlock2 = await fullNodeBlockProvider(blockHash2, testCts.Token);
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
		BlockProvider fullNodeBlockProvider =
			(blockHash, _) =>
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
			};

		var tryGetBlock = BlockProviders.CachedBlockProvider(fullNodeBlockProvider, fileSystemBlockRepository);

		var returnedBlock1 = await tryGetBlock(blockHash1, testCts.Token);
		Assert.Same(block1, returnedBlock1);

		var returnedBlock2 = await tryGetBlock(blockHash2, testCts.Token);
		Assert.Null(returnedBlock2);

		var returnedBlock3 = await tryGetBlock(blockHash3, testCts.Token);
		Assert.Same(block3, returnedBlock3);

		var returnedBlock4 = await tryGetBlock(blockHash4, testCts.Token);
		Assert.Same(block4, returnedBlock4);

		// Second attempt to get block2.
		returnedBlock2 = await tryGetBlock(blockHash2, testCts.Token);
		Assert.Same(block2, returnedBlock2);

		// Getting a block over P2P fails because there is no P2P provider registered.
		var returnedBlock5 = await tryGetBlock(blockHash2, testCts.Token);
		Assert.Null(returnedBlock5);
	}
}
