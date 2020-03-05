using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Threading.Tasks;

namespace WalletWasabi.BitcoinCore
{
	public interface IRPCClient
	{
		Network Network { get; }
		RPCCredentialString CredentialString { get; }

		public Task<uint256> GetBestBlockHashAsync()
		{
			throw new NotImplementedException();
		}

		public Task<Block> GetBlockAsync(uint256 blockId)
		{
			throw new NotImplementedException();
		}

		public Task<Block> GetBlockAsync(uint blockHeight)
		{
			throw new NotImplementedException();
		}

		public Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash)
		{
			throw new NotImplementedException();
		}

		public Task<uint256[]> GenerateAsync(int blockCount)
		{
			throw new NotImplementedException();
		}

		public Task StopAsync()
		{
			throw new NotImplementedException();
		}

		public Task<BlockchainInfo> GetBlockchainInfoAsync()
		{
			throw new NotImplementedException();
		}

		public Task<PeerInfo[]> GetPeersInfoAsync()
		{
			throw new NotImplementedException();
		}
		public Task<TimeSpan> UptimeAsync()
		{
			throw new NotImplementedException();
		}

		public Task<uint256> SendRawTransactionAsync(Transaction transaction)
		{
			throw new NotImplementedException();
		}

		public Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true)
		{
			throw new NotImplementedException();
		}

		public Task<uint256[]> GetRawMempoolAsync()
		{
			throw new NotImplementedException();
		}

		public Task<MempoolAcceptResult> TestMempoolAcceptAsync(Transaction transaction, bool allowHighFees = false)
		{
			throw new NotImplementedException();
		}

		GetTxOutResponse GetTxOut(uint256 txid, int index, bool includeMempool = true)
		{
			throw new NotImplementedException();
		}

		public Task<GetTxOutResponse> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true)
		{
			throw new NotImplementedException();
		}

		IRPCClient PrepareBatch();

		public Task<uint256> SendToAddressAsync(BitcoinAddress address, Money amount, string commentTx = null, string commentDest = null, bool subtractFeeFromAmount = false, bool replaceable = false)
		{
			throw new NotImplementedException();
		}

		public Task<uint256> GetBlockHashAsync(int height)
		{
			throw new NotImplementedException();
		}

		public Task InvalidateBlockAsync(uint256 blockHash)
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

		public Task<Transaction> GetRawTransactionAsync(uint256 txid, bool throwIfNotFound = true)
		{
			throw new NotImplementedException();
		}

		public Task<int> GetBlockCountAsync()
		{
			throw new NotImplementedException();
		}

		public Task<BitcoinAddress> GetNewAddressAsync()
		{
			throw new NotImplementedException();
		}

		public Task<SignRawTransactionResponse> SignRawTransactionWithWalletAsync(SignRawTransactionRequest request)
		{
			throw new NotImplementedException();
		}

		public Task<UnspentCoin[]> ListUnspentAsync()
		{
			throw new NotImplementedException();
		}

		public Task SendBatchAsync()
		{
			throw new NotImplementedException();
		}
	}
}
