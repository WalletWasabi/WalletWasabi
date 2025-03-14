using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.BitcoinRpc.Models;

namespace WalletWasabi.BitcoinRpc;

public interface IRPCClient
{
	Network Network { get; }
	RPCCredentialString CredentialString { get; }

	Task<uint256> GetBestBlockHashAsync(CancellationToken cancellationToken = default);

	Task<Block> GetBlockAsync(uint256 blockId, CancellationToken cancellationToken = default);

	Task<Block> GetBlockAsync(uint blockHeight, CancellationToken cancellationToken = default);

	Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash, CancellationToken cancellationToken = default);

	Task<uint256[]> GenerateAsync(int blockCount, CancellationToken cancellationToken = default);

	Task StopAsync(CancellationToken cancellationToken = default);

	Task<BlockchainInfo> GetBlockchainInfoAsync(CancellationToken cancellationToken = default);

	Task<PeerInfo[]> GetPeersInfoAsync(CancellationToken cancellationToken = default);

	Task<TimeSpan> UptimeAsync(CancellationToken cancellationToken = default);

	Task<uint256> SendRawTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default);

	Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default);

	Task<uint256[]> GetRawMempoolAsync(CancellationToken cancellationToken = default);

	Task<MemPoolInfo> GetMempoolInfoAsync(CancellationToken cancel = default);

	Task<MempoolAcceptResult> TestMempoolAcceptAsync(Transaction transaction, CancellationToken cancellationToken = default);

	Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, CancellationToken cancellationToken = default);

	Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true, CancellationToken cancellationToken = default);

	IRPCClient PrepareBatch();

	Task<uint256> SendToAddressAsync(BitcoinAddress address, Money amount, bool replaceable = false, CancellationToken cancellationToken = default);

	Task<uint256> GetBlockHashAsync(int height, CancellationToken cancellationToken = default);

	Task InvalidateBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default);

	Task AbandonTransactionAsync(uint256 txid /*, CancellationToken cancellationToken*/);

	Task<BumpResponse> BumpFeeAsync(uint256 txid, CancellationToken cancellationToken = default);

	Task<Transaction> GetRawTransactionAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default);

	Task<IEnumerable<Transaction>> GetRawTransactionsAsync(IEnumerable<uint256> txids, CancellationToken cancel);

	Task<int> GetBlockCountAsync(CancellationToken cancellationToken = default);

	Task<BitcoinAddress> GetNewAddressAsync(CancellationToken cancellationToken = default);

	Task<SignRawTransactionResponse> SignRawTransactionWithWalletAsync(SignRawTransactionRequest request, CancellationToken cancellationToken = default);

	Task<UnspentCoin[]> ListUnspentAsync(/*CancellationToken cancellationToken*/);

	Task SendBatchAsync(CancellationToken cancellationToken = default);

	Task<EstimateSmartFeeResponse> TryEstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, CancellationToken cancellationToken = default);

	Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId, CancellationToken cancellationToken = default);

	Task<BlockFilter> GetBlockFilterAsync(uint256 blockId, CancellationToken cancellationToken = default);

	Task<uint256[]> GenerateToAddressAsync(int nBlocks, BitcoinAddress address, CancellationToken cancellationToken = default);

	Task<RPCClient> CreateWalletAsync(string walletNameOrPath, CreateWalletOptions? options = null, CancellationToken cancellationToken = default);
}
