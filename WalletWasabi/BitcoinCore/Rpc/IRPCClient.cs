using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc.Models;

namespace WalletWasabi.BitcoinCore.Rpc
{
	public interface IRPCClient
	{
		Network Network { get; }
		RPCCredentialString CredentialString { get; }

		Task<uint256> GetBestBlockHashAsync();

		Task<Block> GetBlockAsync(uint256 blockId);

		Task<Block> GetBlockAsync(uint blockHeight);

		Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash);

		Task<uint256[]> GenerateAsync(int blockCount);

		Task StopAsync();

		Task<BlockchainInfo> GetBlockchainInfoAsync();

		Task<PeerInfo[]> GetPeersInfoAsync();

		Task<TimeSpan> UptimeAsync();

		Task<uint256> SendRawTransactionAsync(Transaction transaction);

		Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true);

		Task<uint256[]> GetRawMempoolAsync();

		Task<MemPoolInfo> GetMempoolInfoAsync(CancellationToken cancel = default);

		Task<MempoolAcceptResult> TestMempoolAcceptAsync(Transaction transaction);

		Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative);

		Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true);

		IRPCClient PrepareBatch();

		Task<uint256> SendToAddressAsync(BitcoinAddress address, Money amount, bool replaceable = false);

		Task<uint256> GetBlockHashAsync(int height);

		Task InvalidateBlockAsync(uint256 blockHash);

		Task AbandonTransactionAsync(uint256 txid);

		Task<BumpResponse> BumpFeeAsync(uint256 txid);

		Task<Transaction> GetRawTransactionAsync(uint256 txid, bool throwIfNotFound = true);

		Task<IEnumerable<Transaction>> GetRawTransactionsAsync(IEnumerable<uint256> txids, CancellationToken cancel);

		Task<int> GetBlockCountAsync();

		Task<BitcoinAddress> GetNewAddressAsync();

		Task<SignRawTransactionResponse> SignRawTransactionWithWalletAsync(SignRawTransactionRequest request);

		Task<UnspentCoin[]> ListUnspentAsync();

		Task SendBatchAsync();

		Task<EstimateSmartFeeResponse> TryEstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative);

		Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId);

		Task<uint256[]> GenerateToAddressAsync(int nBlocks, BitcoinAddress address);

		Task<RPCClient> CreateWalletAsync(string walletNameOrPath, CreateWalletOptions? options = null);
	}
}
