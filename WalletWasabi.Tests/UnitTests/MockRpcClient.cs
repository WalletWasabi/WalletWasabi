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

		public Task<uint256> GetBestBlockHashAsync(CancellationToken cancellationToken = default)
		{
			return OnGetBestBlockHashAsync();
		}

		public Task<Block> GetBlockAsync(uint256 blockId, CancellationToken cancellationToken = default)
		{
			return OnGetBlockAsync(blockId);
		}

		public Task<Block> GetBlockAsync(uint blockHeight, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<BlockchainInfo> GetBlockchainInfoAsync(CancellationToken cancellationToken = default)
		{
			return OnGetBlockchainInfoAsync();
		}

		public Task<int> GetBlockCountAsync(CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<uint256> GetBlockHashAsync(int height, CancellationToken cancellationToken = default)
		{
			return OnGetBlockHashAsync(height);
		}

		public Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash, CancellationToken cancellationToken = default)
		{
			return OnGetBlockHeaderAsync(blockHash);
		}

		public Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<MemPoolInfo> GetMempoolInfoAsync(CancellationToken cancellationToken = default)
		{
			return OnGetMempoolInfoAsync();
		}

		public Task<BitcoinAddress> GetNewAddressAsync(CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<PeerInfo[]> GetPeersInfoAsync(CancellationToken cancellationToken = default)
		{
			return OnGetPeersInfoAsync();
		}

		public Task<uint256[]> GetRawMempoolAsync(CancellationToken cancellationToken = default)
		{
			return OnGetRawMempoolAsync();
		}

		public Task<Transaction> GetRawTransactionAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default)
		{
			return OnGetRawTransactionAsync(txid, throwIfNotFound);
		}

		public Task<IEnumerable<Transaction>> GetRawTransactionsAsync(IEnumerable<uint256> txids, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true, CancellationToken cancellationToken = default)
		{
			var resp = OnGetTxOutAsync(txid, index, includeMempool);
			return Task.FromResult(resp);
		}

		public Task InvalidateBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<UnspentCoin[]> ListUnspentAsync(/*CancellationToken cancellationToken = default*/)
		{
			throw new NotImplementedException();
		}

		public IRPCClient PrepareBatch()
		{
			return this;
		}

		public Task SendBatchAsync(CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public Task<EstimateSmartFeeResponse> TryEstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId, CancellationToken cancellationToken = default)
		{
			return OnGetVerboseBlockAsync(blockId);
		}

		public Task<uint256> SendRawTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
		{
			var resp = OnSendRawTransactionAsync(transaction);
			return Task.FromResult(resp);
		}

		public Task<uint256> SendToAddressAsync(BitcoinAddress address, Money amount, bool replaceable = false, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<SignRawTransactionResponse> SignRawTransactionWithWalletAsync(SignRawTransactionRequest request, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task StopAsync(CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<MempoolAcceptResult> TestMempoolAcceptAsync(Transaction transaction, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<TimeSpan> UptimeAsync(CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task AbandonTransactionAsync(uint256 txid /*, CancellationToken cancellationToken = default*/)
		{
			throw new NotImplementedException();
		}

		public Task<BumpResponse> BumpFeeAsync(uint256 txid, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<uint256[]> GenerateAsync(int blockCount, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, CancellationToken cancellationToken = default)
		{
			return OnEstimateSmartFeeAsync(confirmationTarget, estimateMode);
		}

		public Task<uint256[]> GenerateToAddressAsync(int nBlocks, BitcoinAddress address, CancellationToken cancellationToken = default)
		{
			return OnGenerateToAddressAsync(nBlocks, address);
		}

		public Task<RPCClient> CreateWalletAsync(string walletNameOrPath, CreateWalletOptions? options = null, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}
	}
}
