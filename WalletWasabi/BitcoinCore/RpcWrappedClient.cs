using NBitcoin;
using NBitcoin.RPC;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore
{
	public class RpcWrappedClient : IRPCClient
	{
		public RpcWrappedClient(RPCClient rpc)
		{
			Rpc = Guard.NotNull(nameof(rpc), rpc);
		}

		public Network Network => Rpc.Network;

		public RPCCredentialString CredentialString => Rpc.CredentialString;

		private RPCClient Rpc { get; }

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

		public IRPCClient PrepareBatch()
		{
			throw new System.NotImplementedException();
		}
	}
}
