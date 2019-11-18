using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
		public async Task GenesisBlockOny()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			var notifier = CreateNotifier(chain);
			var blockAwaiter = new EventAwaiter<Block>(
				h => notifier.OnBlock += h,
				h => notifier.OnBlock -= h);
			var reorgAwaiter = new EventAwaiter<BlockHeader>(
				h => notifier.OnReorg += h,
				h => notifier.OnReorg -= h);

			notifier.Start();

			// No block notifications nor reorg notifications
			await Assert.ThrowsAsync<OperationCanceledException>(() => blockAwaiter.WaitAsync(TimeSpan.FromSeconds(1)));
			await Assert.ThrowsAsync<OperationCanceledException>(() => reorgAwaiter.WaitAsync(TimeSpan.FromSeconds(1)));

			Assert.Equal(Network.RegTest.GenesisHash, notifier.Status);

			await notifier.StopAsync();
		}

		[Fact]
		public async Task HitGenesisBlockDuringInitialization()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			foreach(var n in Enumerable.Range(0, 3))
			{
				await AddBlockAsync(chain);
			}
			var notifier = CreateNotifier(chain);
			var blockAwaiter = new EventAwaiter<Block>(
				h => notifier.OnBlock += h,
				h => notifier.OnBlock -= h);
			var reorgAwaiter = new EventAwaiter<BlockHeader>(
				h => notifier.OnReorg += h,
				h => notifier.OnReorg -= h);

			notifier.Start();

			// No block notifications nor reorg notifications
			await Assert.ThrowsAsync<OperationCanceledException>(() => blockAwaiter.WaitAsync(TimeSpan.FromSeconds(1)));
			await Assert.ThrowsAsync<OperationCanceledException>(() => reorgAwaiter.WaitAsync(TimeSpan.FromSeconds(1)));

			Assert.Equal(chain.Tip.HashBlock, notifier.Status);

			await notifier.StopAsync();
		}

		[Fact]
		public async Task NotifyBlocks()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			var notifier = CreateNotifier(chain);
			var blockCount = 3;

			var reorgAwaiter = new EventAwaiter<BlockHeader>(
				h => notifier.OnReorg += h,
				h => notifier.OnReorg -= h);

			notifier.Start();

			// Assert that the blocks come in the right order
			var height = 0;
			EventHandler<Block> onBlockInv = (s, b) => Assert.Equal(b.GetHash(), chain.GetBlock(height++).HashBlock);
			notifier.OnBlock += onBlockInv;

			foreach(var n in Enumerable.Range(0, blockCount))
			{
				await AddBlockAsync(chain);
			}

			notifier.TriggerRound();
			await Task.Delay(TimeSpan.FromMilliseconds(100)); // give it time to process the blocks

			// Three blocks notifications  
			Assert.Equal(chain.Height, height);

			// No reorg notifications
			await Assert.ThrowsAsync<OperationCanceledException>(() => reorgAwaiter.WaitAsync(TimeSpan.FromSeconds(1)));
			Assert.Equal(chain.Tip.HashBlock, notifier.Status);


			notifier.OnBlock -= onBlockInv;
			await notifier.StopAsync();
		}

		[Fact]
		public async Task SimpleReorg()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			var notifier = CreateNotifier(chain);

			var blockAwaiter = new EventsAwaiter<Block>(
				h => notifier.OnBlock += h,
				h => notifier.OnBlock -= h,
				5);

			var reorgAwaiter = new EventAwaiter<BlockHeader>(
				h => notifier.OnReorg += h,
				h => notifier.OnReorg -= h);

			notifier.Start();

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
			Assert.Equal(forkPoint.HashBlock, reorgedkBlock.HashPrevBlock);
			Assert.Equal(blockToBeReorged.HashBlock, reorgedkBlock.GetHash());
			Assert.Equal(chain.Tip.HashBlock, notifier.Status);

			await notifier.StopAsync();
		}

		[Fact]
		public async Task LongChainReorg()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			var notifier = CreateNotifier(chain);

			var blockAwaiter = new EventsAwaiter<Block>(
				h => notifier.OnBlock += h,
				h => notifier.OnBlock -= h,
				11);

			var reorgAwaiter = new EventsAwaiter<BlockHeader>(
				h => notifier.OnReorg += h,
				h => notifier.OnReorg -= h,
				3);

			notifier.Start();

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
			var expectedReorgedBlocks = firstReorgedChain.ToList().Concat(new[]{ secondReorgedChain[2]});
			Assert.Subset(reorgedkBlock.Select(x=>x.GetHash()).ToHashSet(), expectedReorgedBlocks.Select(x=>x.Header.GetHash()).ToHashSet());
			Assert.Equal(chain.Tip.HashBlock, notifier.Status);

			await notifier.StopAsync();
		}


		[Fact]
		public async Task SuperFastNodeValidation()
		{
			var chain = new ConcurrentChain(Network.RegTest);
			var notifier = CreateNotifier(chain);
			var blockAwaiter = new EventsAwaiter<Block>(
				h => notifier.OnBlock += h,
				h => notifier.OnBlock -= h,
				144);

			notifier.Start();

			var lastKnownBlock = await AddBlockAsync(chain);

			foreach(var i in Enumerable.Range(0, 200))
			{
				await AddBlockAsync(chain, wait: false);
			}
			await AddBlockAsync(chain, wait: true);
			notifier.TriggerRound();
			
			Assert.Equal(chain.Tip.HashBlock, notifier.Status);

			var nofifiedBlocks = (await blockAwaiter.WaitAsync(TimeSpan.FromSeconds(1))).ToArray();
			
			var tip = chain.Tip;
			var pos = nofifiedBlocks.Length -1;
			while( tip.HashBlock != nofifiedBlocks[pos].GetHash())
			{
				tip = tip.Previous;
			}

			while(pos >= 0)
			{
				Assert.Equal(tip.HashBlock, nofifiedBlocks[pos].GetHash());
				tip = tip.Previous;
				pos--;
			}

			await notifier.StopAsync();
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

	class MockRpcClient : IRPCClient
	{
		public Func<Task<uint256>> OnGetBestBlockHashAsync { get; set; }
		public Func<uint256, Task<Block>> OnGetBlockAsync { get; set; }
		public Func<uint256, Task<BlockHeader>> OnGetBlockHeaderAsync { get; set; }

		public Network Network => Network.RegTest;

		public Task<uint256> GetBestBlockHashAsync()
		{
			return OnGetBestBlockHashAsync();;
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