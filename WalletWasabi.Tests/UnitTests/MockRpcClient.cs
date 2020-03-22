using System;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.BitcoinCore;

namespace WalletWasabi.Tests.UnitTests
{
	internal class MockRpcClient : IRPCClient
	{
		public Func<Task<uint256>> OnGetBestBlockHashAsync { get; set; }
		public Func<uint256, Task<Block>> OnGetBlockAsync { get; set; }
		public Func<uint256, Task<BlockHeader>> OnGetBlockHeaderAsync { get; set; }

		public Func<Task<BlockchainInfo>> OnGetBlockchainInfoAsync { get; set; }

		public Func<RPCOperations, object[], Task<RPCResponse>> OnSendCommandAsync { get; set; }

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

		public Task<uint256> GetBlockHashAsync(uint height)
		{
			throw new NotImplementedException();
		}

		public Task<BlockchainInfo> GetBlockchainInfoAsync()
		{
			return OnGetBlockchainInfoAsync();
		}

		public Task<RPCResponse> SendCommandAsync(RPCOperations operation, params object[] p)
		{
			return OnSendCommandAsync(operation, p);
		}
	}
}
