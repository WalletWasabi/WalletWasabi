using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.BitcoinCore.Rpc.Models;

namespace WalletWasabi.Tests.UnitTests
{
	public class MockRpcClient : IRPCClient
	{
		public Func<Task<uint256>> OnGetBestBlockHashAsync { get; set; }
		public Func<uint256, int, bool, GetTxOutResponse?> OnGetTxOutAsync { get; set; }
		public Func<uint256, Task<Block>> OnGetBlockAsync { get; set; }
		public Func<int, Task<uint256>> OnGetBlockHashAsync { get; set; }
		public Func<uint256, Task<BlockHeader>> OnGetBlockHeaderAsync { get; set; }
		public Func<Task<BlockchainInfo>> OnGetBlockchainInfoAsync { get; set; }
		public Func<uint256, Task<VerboseBlockInfo>> OnGetVerboseBlockAsync { get; set; }
		public Func<Transaction, uint256> OnSendRawTransactionAsync { get; set; }
		public Func<Task<MemPoolInfo>> OnGetMempoolInfoAsync { get; set; }
		public Func<Task<uint256[]>> OnGetRawMempoolAsync { get; set; }
		public Func<uint256, bool, Task<Transaction>> OnGetRawTransactionAsync { get; set; }
		public Func<int, EstimateSmartFeeMode, Task<EstimateSmartFeeResponse>> OnEstimateSmartFeeAsync { get; set; }
		public Func<Task<PeerInfo[]>> OnGetPeersInfoAsync { get; set; }
		public Func<int, BitcoinAddress, Task<uint256[]>> OnGenerateToAddressAsync { get; set; }
		public Network Network { get; set; } = Network.RegTest;
		public RPCCredentialString CredentialString => new();

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

		public Task<MemPoolInfo> GetMempoolInfoAsync(CancellationToken cancel = default)
		{
			return OnGetMempoolInfoAsync();
		}

		public Task<BitcoinAddress> GetNewAddressAsync()
		{
			throw new NotImplementedException();
		}

		public Task<PeerInfo[]> GetPeersInfoAsync()
		{
			return OnGetPeersInfoAsync();
		}

		public Task<uint256[]> GetRawMempoolAsync()
		{
			return OnGetRawMempoolAsync();
		}

		public Task<Transaction> GetRawTransactionAsync(uint256 txid, bool throwIfNotFound = true)
		{
			return OnGetRawTransactionAsync(txid, throwIfNotFound);
		}

		public Task<IEnumerable<Transaction>> GetRawTransactionsAsync(IEnumerable<uint256> txids, CancellationToken cancel)
		{
			throw new NotImplementedException();
		}

		public Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true)
		{
			var resp = OnGetTxOutAsync(txid, index, includeMempool);
			return Task.FromResult(resp);
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
			return this;
		}

		public Task SendBatchAsync()
		{
			return Task.CompletedTask;
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
			var resp = OnSendRawTransactionAsync(transaction);
			return Task.FromResult(resp);
		}

		public Task<uint256> SendToAddressAsync(BitcoinAddress address, Money amount, bool replaceable = false)
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

		public Task<MempoolAcceptResult> TestMempoolAcceptAsync(Transaction transaction)
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
			return OnEstimateSmartFeeAsync(confirmationTarget, estimateMode);
		}

		public Task<uint256[]> GenerateToAddressAsync(int nBlocks, BitcoinAddress address)
		{
			return OnGenerateToAddressAsync(nBlocks, address);
		}

		public Task<RPCClient> CreateWalletAsync(string walletNameOrPath, CreateWalletOptions? options = null)
		{
			throw new NotImplementedException();
		}
	}
}
