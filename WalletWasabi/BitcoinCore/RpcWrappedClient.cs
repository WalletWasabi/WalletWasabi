using NBitcoin;
using NBitcoin.RPC;
using System.Threading.Tasks;

namespace WalletWasabi.BitcoinCore
{
	public class RpcWrappedClient : IRPCClient
	{
		private RPCClient Rpc { get; }

		public RpcWrappedClient(RPCClient rpc)
		{
			Rpc = rpc;
		}

		public Network Network => Rpc.Network;

		public async Task<uint256> GetBestBlockHashAsync()
		{
			return await Rpc.GetBestBlockHashAsync().ConfigureAwait(false);
		}

		public async Task<Block> GetBlockAsync(uint256 blockId)
		{
			return await Rpc.GetBlockAsync(blockId).ConfigureAwait(false);
		}

		public async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash)
		{
			return await Rpc.GetBlockHeaderAsync(blockHash).ConfigureAwait(false);
		}
	}
}
