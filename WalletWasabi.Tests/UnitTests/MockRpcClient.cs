using System;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.RpcModels;

namespace WalletWasabi.Tests.UnitTests
{
	internal class MockRpcClient : IRPCClient
	{
		public Func<Task<uint256>> OnGetBestBlockHashAsync { get; set; }
		public Func<uint256, Task<Block>> OnGetBlockAsync { get; set; }
		public Func<int, Task<uint256>> OnGetBlockHashAsync { get; set; }
		public Func<uint256, Task<BlockHeader>> OnGetBlockHeaderAsync { get; set; }
		public Func<Task<BlockchainInfo>> OnGetBlockchainInfoAsync { get; set; }
		public Func<uint256, Task<VerboseBlockInfo>> OnGetVerboseBlockAsync { get; set; }

		public Network Network { get; set; } = Network.RegTest;
		public RPCCredentialString CredentialString => new RPCCredentialString();

		public Task<uint256> GetBestBlockHashAsync()
		{
			return OnGetBestBlockHashAsync();
		}

		public Task<Block> GetBlockAsync(uint256 blockId)
		{
			return OnGetBlockAsync(blockId);
		}

		public Task<Block> GetBlockAsync(uint blockHeight)
		{
			throw new NotImplementedException();
		}

		public Task<BlockchainInfo> GetBlockchainInfoAsync()
		{
			return OnGetBlockchainInfoAsync();
		}

		public Task<int> GetBlockCountAsync()
		{
			throw new NotImplementedException();
		}

		public Task<uint256> GetBlockHashAsync(int height)
		{
			return OnGetBlockHashAsync(height);
		}

		public Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash)
		{
			return OnGetBlockHeaderAsync(blockHash);
		}

		public Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true)
		{
			throw new NotImplementedException();
		}

		public Task<BitcoinAddress> GetNewAddressAsync()
		{
			throw new NotImplementedException();
		}

		public Task<PeerInfo[]> GetPeersInfoAsync()
		{
			throw new NotImplementedException();
		}

		public Task<uint256[]> GetRawMempoolAsync()
		{
			throw new NotImplementedException();
		}

		public Task<Transaction> GetRawTransactionAsync(uint256 txid, bool throwIfNotFound = true)
		{
			throw new NotImplementedException();
		}

		public GetTxOutResponse GetTxOut(uint256 txid, int index, bool includeMempool = true)
		{
			throw new NotImplementedException();
		}

		public Task<GetTxOutResponse> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true)
		{
			throw new NotImplementedException();
		}

		public Task InvalidateBlockAsync(uint256 blockHash)
		{
			throw new NotImplementedException();
		}

		public Task<UnspentCoin[]> ListUnspentAsync()
		{
			throw new NotImplementedException();
		}

		public IRPCClient PrepareBatch()
		{
			throw new NotImplementedException();
		}

		public Task SendBatchAsync()
		{
			throw new NotImplementedException();
		}

		public Task<EstimateSmartFeeResponse> TryEstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative)
		{
			throw new NotImplementedException();
		}

		public Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId)
		{
			return OnGetVerboseBlockAsync(blockId);
		}

		public Task<uint256> SendRawTransactionAsync(Transaction transaction)
		{
			throw new NotImplementedException();
		}

		public Task<uint256> SendToAddressAsync(BitcoinAddress address, Money amount, string commentTx = null, string commentDest = null, bool subtractFeeFromAmount = false, bool replaceable = false)
		{
			throw new NotImplementedException();
		}

		public Task<SignRawTransactionResponse> SignRawTransactionWithWalletAsync(SignRawTransactionRequest request)
		{
			throw new NotImplementedException();
		}

		public Task StopAsync()
		{
			throw new NotImplementedException();
		}

		public Task<MempoolAcceptResult> TestMempoolAcceptAsync(Transaction transaction, bool allowHighFees = false)
		{
			throw new NotImplementedException();
		}

		public Task<TimeSpan> UptimeAsync()
		{
			throw new NotImplementedException();
		}

		public Task AbandonTransactionAsync(uint256 txid)
		{
			throw new NotImplementedException();
		}

		public Task<BumpResponse> BumpFeeAsync(uint256 txid)
		{
			throw new NotImplementedException();
		}

		public Task<uint256[]> GenerateAsync(int blockCount)
		{
			throw new NotImplementedException();
		}

		public Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative)
		{
			throw new NotImplementedException();
		}
	}
}
