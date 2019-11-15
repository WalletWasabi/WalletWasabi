using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Services;

namespace WalletWasabi.Blockchain.Blocks
{
	public class BlockNotifier : PeriodicRunner<uint256>
	{
		public event EventHandler<Block> OnBlock;

		public event EventHandler<BlockHeader> OnReorg;

		public RPCClient RpcClient { get; set; }
		public TrustedNodeNotifyingBehavior P2pNotifier { get; }

		public BlockNotifier(TimeSpan period, RPCClient rpcClient, TrustedNodeNotifyingBehavior p2pNotifier) : base(period, null)
		{
			RpcClient = Guard.NotNull(nameof(rpcClient), rpcClient);
			P2pNotifier = Guard.NotNull(nameof(p2pNotifier), p2pNotifier);

			P2pNotifier.BlockInv += P2pNotifier_BlockInv;
		}

		private void P2pNotifier_BlockInv(object sender, uint256 e)
		{
			throw new NotImplementedException();
		}

		public override async Task<uint256> ActionAsync(CancellationToken cancel)
		{
			throw new NotImplementedException();
		}

		public new async Task StopAsync()
		{
			P2pNotifier.BlockInv -= P2pNotifier_BlockInv;
			await base.StopAsync().ConfigureAwait(false);
		}
	}
}
