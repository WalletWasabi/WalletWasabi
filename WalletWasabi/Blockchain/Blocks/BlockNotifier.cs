using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.P2p;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Blockchain.Blocks
{
	public class BlockNotifier : PeriodicRunner<uint256>
	{
		public event EventHandler<Block> OnBlock;

		public event EventHandler<BlockHeader> OnReorg;

		public RPCClient RpcClient { get; set; }
		public TrustedP2pBehavior TrustedP2pBehavior { get; }
		private List<BlockHeader> ProcessedBlocks { get; }

		public BlockNotifier(TimeSpan period, RPCClient rpcClient, TrustedP2pBehavior trustedP2pBehavior) : base(period, null)
		{
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
			TrustedP2pBehavior = Guard.NotNull(nameof(trustedP2pBehavior), trustedP2pBehavior);

			ProcessedBlocks = new List<BlockHeader>();

			TrustedP2pBehavior.BlockInv += P2pNotifier_BlockInv;
		}

		private void P2pNotifier_BlockInv(object sender, uint256 blockHash)
		{
			TriggerRound();
		}

		protected override async Task<uint256> ActionAsync(CancellationToken cancel)
		{
			var bestBlockHash = await RpcClient.GetBestBlockHashAsync().ConfigureAwait(false);

			// If there's no new block.
			// Don't notify about the genesis block.
			if (bestBlockHash == Status || bestBlockHash == RpcClient.Network.GenesisHash)
			{
				return bestBlockHash;
			}

			var arrivedBlock = await RpcClient.GetBlockAsync(bestBlockHash).ConfigureAwait(false);
			var arrivedHeader = arrivedBlock.Header;
			arrivedHeader.PrecomputeHash(false, true);

			// If we haven't processed any block yet then we're processing it without checks.
			if (!ProcessedBlocks.Any())
			{
				AddBlock(arrivedBlock);
				return bestBlockHash;
			}

			// If block was already processed return.
			if (ProcessedBlocks.Any(x => x.GetHash() == arrivedHeader.GetHash()))
			{
				return bestBlockHash;
			}

			// If this block follows the proper order then add.
			if (ProcessedBlocks.Last().GetHash() == arrivedHeader.HashPrevBlock)
			{
				AddBlock(arrivedBlock);
				return bestBlockHash;
			}

			// Else let's sort out things.
			var foundPrevBlock = ProcessedBlocks.FirstOrDefault(x => x.GetHash() == arrivedHeader.HashPrevBlock);
			// Missed notifications on some previous blocks.
			if (foundPrevBlock != null)
			{
				// Reorg happened.
				ReorgToBlock(foundPrevBlock);
				AddBlock(arrivedBlock);
				return bestBlockHash;
			}

			var missedBlocks = new List<Block>
				{
					arrivedBlock
				};
			var currentHeader = arrivedHeader;
			while (true)
			{
				Block missedBlock = missedBlock = await RpcClient.GetBlockAsync(currentHeader.HashPrevBlock).ConfigureAwait(false);

				currentHeader = missedBlock.Header;
				currentHeader.PrecomputeHash(false, true);
				missedBlocks.Add(missedBlock);

				if (missedBlocks.Count > 100)
				{
					var processedBlocksClone = ProcessedBlocks.ToArray();
					var processedReversedBlocks = processedBlocksClone.Reverse();
					ProcessedBlocks.Clear();
					foreach (var processedBlock in processedReversedBlocks)
					{
						OnReorg?.Invoke(this, processedBlock);
					}
					Logger.LogCritical("A reorg detected over 100 blocks. Wasabi cannot handle that.");
					break;
				}

				// If we found the proper chain.
				foundPrevBlock = ProcessedBlocks.FirstOrDefault(x => x.GetHash() == currentHeader.HashPrevBlock);
				if (foundPrevBlock != null)
				{
					// If the last block hash is not what we found, then we missed a reorg also.
					if (foundPrevBlock.GetHash() != ProcessedBlocks.Last().GetHash())
					{
						ReorgToBlock(foundPrevBlock);
					}

					break;
				}
			}

			missedBlocks.Reverse();
			foreach (var b in missedBlocks)
			{
				AddBlock(b);
			}

			return bestBlockHash;
		}

		private void AddBlock(Block block)
		{
			ProcessedBlocks.Add(block.Header);
			OnBlock?.Invoke(this, block);
		}

		private void ReorgToBlock(BlockHeader correctBlock)
		{
			var index = ProcessedBlocks.IndexOf(correctBlock);
			int countToRemove = ProcessedBlocks.Count - (index + 1);
			var toRemoves = ProcessedBlocks.TakeLast(countToRemove).ToList();
			ProcessedBlocks.RemoveRange(index + 1, countToRemove);
			toRemoves.Reverse();
			foreach (var toRemove in toRemoves)
			{
				OnReorg?.Invoke(this, toRemove);
			}
		}

		public new async Task StopAsync()
		{
			TrustedP2pBehavior.BlockInv -= P2pNotifier_BlockInv;
			await base.StopAsync().ConfigureAwait(false);
		}
	}
}
