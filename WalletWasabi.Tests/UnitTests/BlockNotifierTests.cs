using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class BlockNotifierTests
	{
		[Fact]
		public async Task GenesisBlockOnlyAsync()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			using var notifier = CreateNotifier(chain);
			var blockAwaiter = new EventAwaiter<Block>(
				h => notifier.OnBlock += h,
				h => notifier.OnBlock -= h);
			var reorgAwaiter = new EventAwaiter<uint256>(
				h => notifier.OnReorg += h,
				h => notifier.OnReorg -= h);

			await notifier.StartAsync(CancellationToken.None);

			// No block notifications nor reorg notifications
			await Assert.ThrowsAsync<OperationCanceledException>(() => blockAwaiter.WaitAsync(TimeSpan.FromSeconds(1)));
			await Assert.ThrowsAsync<OperationCanceledException>(() => reorgAwaiter.WaitAsync(TimeSpan.FromSeconds(1)));

			Assert.Equal(Network.RegTest.GenesisHash, notifier.BestBlockHash);

			await notifier.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task HitGenesisBlockDuringInitializationAsync()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			foreach (var n in Enumerable.Range(0, 3))
			{
				await AddBlockAsync(chain);
			}
			using var notifier = CreateNotifier(chain);
			var blockAwaiter = new EventAwaiter<Block>(
				h => notifier.OnBlock += h,
				h => notifier.OnBlock -= h);
			var reorgAwaiter = new EventAwaiter<uint256>(
				h => notifier.OnReorg += h,
				h => notifier.OnReorg -= h);

			await notifier.StartAsync(CancellationToken.None);

			// No block notifications nor reorg notifications
			await Assert.ThrowsAsync<OperationCanceledException>(() => blockAwaiter.WaitAsync(TimeSpan.FromSeconds(1)));
			await Assert.ThrowsAsync<OperationCanceledException>(() => reorgAwaiter.WaitAsync(TimeSpan.FromSeconds(1)));

			Assert.Equal(chain.Tip.HashBlock, notifier.BestBlockHash);

			await notifier.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task NotifyBlocksAsync()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			using var notifier = CreateNotifier(chain);
			var blockCount = 3;

			var reorgAwaiter = new EventAwaiter<uint256>(
				h => notifier.OnReorg += h,
				h => notifier.OnReorg -= h);

			await notifier.StartAsync(CancellationToken.None);

			// Assert that the blocks come in the right order
			var height = 0;
			void OnBlockInv(object s, Block b) => Assert.Equal(b.GetHash(), chain.GetBlock(height++).HashBlock);
			notifier.OnBlock += OnBlockInv;

			foreach (var n in Enumerable.Range(0, blockCount))
			{
				await AddBlockAsync(chain);
			}

			notifier.TriggerRound();
			await Task.Delay(TimeSpan.FromMilliseconds(100)); // give it time to process the blocks

			// Three blocks notifications
			Assert.Equal(chain.Height, height);

			// No reorg notifications
			await Assert.ThrowsAsync<OperationCanceledException>(() => reorgAwaiter.WaitAsync(TimeSpan.FromSeconds(1)));
			Assert.Equal(chain.Tip.HashBlock, notifier.BestBlockHash);

			notifier.OnBlock -= OnBlockInv;
			await notifier.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task SimpleReorgAsync()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			using var notifier = CreateNotifier(chain);

			var blockAwaiter = new EventsAwaiter<Block>(
				h => notifier.OnBlock += h,
				h => notifier.OnBlock -= h,
				5);

			var reorgAwaiter = new EventAwaiter<uint256>(
				h => notifier.OnReorg += h,
				h => notifier.OnReorg -= h);

			await notifier.StartAsync(CancellationToken.None);

			await AddBlockAsync(chain);
			var forkPoint = chain.Tip;
			var blockToBeReorged = await AddBlockAsync(chain);

			chain.SetTip(forkPoint);
			await AddBlockAsync(chain, wait: false);
			await AddBlockAsync(chain, wait: false);
			await AddBlockAsync(chain);
			notifier.TriggerRound();

			// Three blocks notifications
			await blockAwaiter.WaitAsync(TimeSpan.FromSeconds(2));

			// No reorg notifications
			var reorgedkBlock = await reorgAwaiter.WaitAsync(TimeSpan.FromSeconds(1));
			Assert.Equal(blockToBeReorged.HashBlock, reorgedkBlock);
			Assert.Equal(chain.Tip.HashBlock, notifier.BestBlockHash);

			await notifier.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task LongChainReorgAsync()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			using var notifier = CreateNotifier(chain);

			var blockAwaiter = new EventsAwaiter<Block>(
				h => notifier.OnBlock += h,
				h => notifier.OnBlock -= h,
				11);

			var reorgAwaiter = new EventsAwaiter<uint256>(
				h => notifier.OnReorg += h,
				h => notifier.OnReorg -= h,
				3);

			await notifier.StartAsync(CancellationToken.None);

			await AddBlockAsync(chain);

			var forkPoint = chain.Tip;
			var firstReorgedChain = new[]
			{
				await AddBlockAsync(chain, wait: false),
				await AddBlockAsync(chain)
			};

			chain.SetTip(forkPoint);
			var secondReorgedChain = new[]
			{
				await AddBlockAsync(chain, wait: false),
				await AddBlockAsync(chain, wait: false),
				await AddBlockAsync(chain)
			};

			chain.SetTip(secondReorgedChain[1]);
			await AddBlockAsync(chain, wait: false);
			await AddBlockAsync(chain, wait: false);
			await AddBlockAsync(chain, wait: false);
			await AddBlockAsync(chain, wait: false);
			await AddBlockAsync(chain);

			// Three blocks notifications
			await blockAwaiter.WaitAsync(TimeSpan.FromSeconds(2));

			// No reorg notifications
			var reorgedkBlock = await reorgAwaiter.WaitAsync(TimeSpan.FromSeconds(1));
			var expectedReorgedBlocks = firstReorgedChain.ToList().Concat(new[] { secondReorgedChain[2] });
			Assert.Subset(reorgedkBlock.ToHashSet(), expectedReorgedBlocks.Select(x => x.Header.GetHash()).ToHashSet());
			Assert.Equal(chain.Tip.HashBlock, notifier.BestBlockHash);

			await notifier.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task SuperFastNodeValidationAsync()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			using var notifier = CreateNotifier(chain);
			var blockAwaiter = new EventsAwaiter<Block>(
				h => notifier.OnBlock += h,
				h => notifier.OnBlock -= h,
				144);

			await notifier.StartAsync(CancellationToken.None);

			var lastKnownBlock = await AddBlockAsync(chain);

			foreach (var i in Enumerable.Range(0, 200))
			{
				await AddBlockAsync(chain, wait: false);
			}
			await AddBlockAsync(chain, wait: true);
			notifier.TriggerRound();

			Assert.Equal(chain.Tip.HashBlock, notifier.BestBlockHash);

			var nofifiedBlocks = (await blockAwaiter.WaitAsync(TimeSpan.FromSeconds(1))).ToArray();

			var tip = chain.Tip;
			var pos = nofifiedBlocks.Length - 1;
			while (tip.HashBlock != nofifiedBlocks[pos].GetHash())
			{
				tip = tip.Previous;
			}

			while (pos >= 0)
			{
				Assert.Equal(tip.HashBlock, nofifiedBlocks[pos].GetHash());
				tip = tip.Previous;
				pos--;
			}

			await notifier.StopAsync(CancellationToken.None);
		}

		private BlockNotifier CreateNotifier(ConcurrentChain chain)
		{
			var rpc = new MockRpcClient();
			rpc.OnGetBestBlockHashAsync = () => Task.FromResult(chain.Tip.HashBlock);
			rpc.OnGetBlockAsync = (blockHash) => Task.FromResult(Block.CreateBlock(chain.GetBlock(blockHash).Header, rpc.Network));
			rpc.OnGetBlockHeaderAsync = (blockHash) => Task.FromResult(chain.GetBlock(blockHash).Header);

			var notifier = new BlockNotifier(TimeSpan.FromMilliseconds(100), rpc);
			return notifier;
		}

		private async Task<ChainedBlock> AddBlockAsync(ConcurrentChain chain, bool wait = true)
		{
			BlockHeader header = Network.RegTest.Consensus.ConsensusFactory.CreateBlockHeader();
			header.Nonce = RandomUtils.GetUInt32();
			header.HashPrevBlock = chain.Tip.HashBlock;
			chain.SetTip(header);
			var block = chain.GetBlock(header.GetHash());
			if (wait)
			{
				await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
			}
			return block;
		}
	}

	internal class MockRpcClient : IRPCClient
	{
		public Func<Task<uint256>> OnGetBestBlockHashAsync { get; set; }
		public Func<uint256, Task<Block>> OnGetBlockAsync { get; set; }
		public Func<uint256, Task<BlockHeader>> OnGetBlockHeaderAsync { get; set; }

		public Network Network => Network.RegTest;

		public Task<uint256> GetBestBlockHashAsync()
		{
			return OnGetBestBlockHashAsync();
		}

		public Task<Block> GetBlockAsync(uint256 blockId)
		{
			return OnGetBlockAsync(blockId);
		}

		public Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash)
		{
			return OnGetBlockHeaderAsync(blockHash);
		}
	}
}
