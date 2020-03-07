using NBitcoin;
using NBitcoin.RPC;
using System.Threading.Tasks;

namespace WalletWasabi.BitcoinCore
{
	public interface IRPCClient
	{
		Network Network { get; }

		Task<uint256> GetBestBlockHashAsync();

		Task<Block> GetBlockAsync(uint256 blockId);

		Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash);

		Task<RPCResponse> SendCommandAsync(RPCOperations operation, params object[] p);
	}
}
