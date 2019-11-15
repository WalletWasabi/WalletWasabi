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
		public TrustedNodeNotifyingBehavior P2pNotifier { get; }
		private List<BlockHeader> ProcessedBlocks { get; }
		private AsyncLock Lock { get; } = new AsyncLock();

		public BlockNotifier(TimeSpan period, RPCClient rpcClient, TrustedNodeNotifyingBehavior p2pNotifier) : base(period, null)
		{
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
			P2pNotifier = Guard.NotNull(nameof(p2pNotifier), p2pNotifier);

			ProcessedBlocks = new List<BlockHeader>();

			P2pNotifier.BlockInv += P2pNotifier_BlockInv;
		}

		private void P2pNotifier_BlockInv(object sender, uint256 blockHash)
		{
		}

		public override async Task<uint256> ActionAsync(CancellationToken cancel)
		{
			using (await Lock.LockAsync().ConfigureAwait(false))
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

				return bestBlockHash;
			}
		}

		private void AddBlock(Block block)
		{
			ProcessedBlocks.Add(block.Header);
			OnBlock?.Invoke(this, block);
		}

		public new async Task StopAsync()
		{
			P2pNotifier.BlockInv -= P2pNotifier_BlockInv;
			await base.StopAsync().ConfigureAwait(false);
		}
	}
}
