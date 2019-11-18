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

		public Task<uint256> GetBestBlockHashAsync()
		{
			return Rpc.GetBestBlockHashAsync();
		}

		public Task<Block> GetBlockAsync(uint256 blockId)
		{
			return Rpc.GetBlockAsync(blockId);
		}

		public Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash)
		{
			return Rpc.GetBlockHeaderAsync(blockHash);
		}
	}
}