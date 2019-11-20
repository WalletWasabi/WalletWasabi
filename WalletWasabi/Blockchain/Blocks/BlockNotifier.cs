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
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.P2p;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Blockchain.Blocks
{
	public class BlockNotifier : PeriodicRunner
	{
		public event EventHandler<Block> OnBlock;

		public event EventHandler<BlockHeader> OnReorg;

		public IRPCClient RpcClient { get; set; }
		public Network Network => RpcClient.Network;
		private List<BlockHeader> ProcessedBlocks { get; }
		public P2pNode P2pNode { get; }
		public uint256 BestBlockHash { get; private set; }

		public BlockNotifier(TimeSpan period, IRPCClient rpcClient, P2pNode p2pNode = null) : base(period)
		{
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
			P2pNode = p2pNode;
			ProcessedBlocks = new List<BlockHeader>();
			if (p2pNode is { })
			{
				p2pNode.BlockInv += P2pNode_BlockInv;
			}
		}

		private void P2pNode_BlockInv(object sender, uint256 blockHash)
		{
			TriggerRound();
		}

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			var bestBlockHash = await RpcClient.GetBestBlockHashAsync().ConfigureAwait(false);

			// If there's no new block.
			if (bestBlockHash == BestBlockHash)
			{
				return;
			}

			var arrivedBlock = await RpcClient.GetBlockAsync(bestBlockHash).ConfigureAwait(false);
			var arrivedHeader = arrivedBlock.Header;
			arrivedHeader.PrecomputeHash(false, true);

			// If we haven't processed any block yet then we're processing the first seven to avoid accidental reogs.
			// 7 blocks, because
			//   - That was the largest recorded reorg so far.
			//   - Reorg in this point of time would be very unlikely anyway.
			//   - 100 blocks would be the sure, but that'd be a huge performance overkill.
			if (!ProcessedBlocks.Any())
			{
				var reorgProtection7Headers = new List<BlockHeader>()
				{
					arrivedHeader
				};

				var currentHeader = arrivedHeader;
				while (reorgProtection7Headers.Count < 7 && currentHeader.GetHash() != Network.GenesisHash)
				{
					currentHeader = await RpcClient.GetBlockHeaderAsync(currentHeader.HashPrevBlock).ConfigureAwait(false);
					reorgProtection7Headers.Add(currentHeader);
				}

				reorgProtection7Headers.Reverse();
				foreach (var header in reorgProtection7Headers)
				{
					// It's initialization. Don't notify about it.
					AddHeader(header);
				}

				BestBlockHash = bestBlockHash;
				return;
			}

			// If block was already processed return.
			if (ProcessedBlocks.Any(x => x.GetHash() == arrivedHeader.GetHash()))
			{
				BestBlockHash = bestBlockHash;
				return;
			}

			// If this block follows the proper order then add.
			if (ProcessedBlocks.Last().GetHash() == arrivedHeader.HashPrevBlock)
			{
				AddBlock(arrivedBlock);
				BestBlockHash = bestBlockHash;
				return;
			}

			// Else let's sort out things.
			var foundPrevBlock = ProcessedBlocks.FirstOrDefault(x => x.GetHash() == arrivedHeader.HashPrevBlock);
			// Missed notifications on some previous blocks.
			if (foundPrevBlock != null)
			{
				// Reorg happened.
				ReorgToBlock(foundPrevBlock);
				AddBlock(arrivedBlock);
				BestBlockHash = bestBlockHash;
				return;
			}

			await HandleMissedBlocksAsync(arrivedBlock);

			BestBlockHash = bestBlockHash;
			return;
		}

		private async Task HandleMissedBlocksAsync(Block arrivedBlock)
		{
			List<Block> missedBlocks = new List<Block>
			{
				arrivedBlock
			};
			var currentHeader = arrivedBlock.Header;
			while (true)
			{
				Block missedBlock = await RpcClient.GetBlockAsync(currentHeader.HashPrevBlock).ConfigureAwait(false);

				if (missedBlocks.Count > 144)
				{
					missedBlocks.RemoveFirst();
				}

				currentHeader = missedBlock.Header;
				currentHeader.PrecomputeHash(false, true);
				missedBlocks.Add(missedBlock);

				if (currentHeader.GetHash() == Network.GenesisHash)
				{
					var processedBlocksClone = ProcessedBlocks.ToArray();
					var processedReversedBlocks = processedBlocksClone.Reverse();
					ProcessedBlocks.Clear();
					foreach (var processedBlock in processedReversedBlocks)
					{
						OnReorg?.Invoke(this, processedBlock);
					}
					break;
				}

				// If we found the proper chain.
				var foundPrevBlock = ProcessedBlocks.FirstOrDefault(x => x.GetHash() == currentHeader.HashPrevBlock);
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
		}

		private void AddBlock(Block block)
		{
			AddHeader(block.Header);
			OnBlock?.Invoke(this, block);
		}

		private void AddHeader(BlockHeader block)
		{
			ProcessedBlocks.Add(block);
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
			if (P2pNode is { })
			{
				P2pNode.BlockInv -= P2pNode_BlockInv;
			}

			await base.StopAsync().ConfigureAwait(false);
		}
	}
}
