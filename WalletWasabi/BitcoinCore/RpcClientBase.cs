using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.RpcModels;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore
{
	public class RpcClientBase : IRPCClient
	{
		public RpcClientBase(RPCClient rpc)
		{
			Rpc = Guard.NotNull(nameof(rpc), rpc);
		}

		public Network Network => Rpc.Network;

		private RPCClient Rpc { get; }

		public RPCCredentialString CredentialString => Rpc.CredentialString;

		public virtual async Task<uint256> GetBestBlockHashAsync()
		{
			return await Rpc.GetBestBlockHashAsync().ConfigureAwait(false);
		}

		public virtual async Task<Block> GetBlockAsync(uint256 blockHash)
		{
			return await Rpc.GetBlockAsync(blockHash).ConfigureAwait(false);
		}

		public virtual async Task<Block> GetBlockAsync(uint blockHeight)
		{
			return await Rpc.GetBlockAsync(blockHeight).ConfigureAwait(false);
		}

		public virtual async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash)
		{
			return await Rpc.GetBlockHeaderAsync(blockHash).ConfigureAwait(false);
		}

		public virtual async Task<BlockchainInfo> GetBlockchainInfoAsync()
		{
			return await Rpc.GetBlockchainInfoAsync().ConfigureAwait(false);
		}

		public virtual async Task<PeerInfo[]> GetPeersInfoAsync()
		{
			return await Rpc.GetPeersInfoAsync().ConfigureAwait(false);
		}

		public virtual async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true)
		{
			return await Rpc.GetMempoolEntryAsync(txid, throwIfNotFound).ConfigureAwait(false);
		}

		public virtual async Task<uint256[]> GetRawMempoolAsync()
		{
			return await Rpc.GetRawMempoolAsync().ConfigureAwait(false);
		}

		public virtual GetTxOutResponse GetTxOut(uint256 txid, int index, bool includeMempool = true)
		{
			return Rpc.GetTxOut(txid, index, includeMempool);
		}

		public virtual async Task<MempoolAcceptResult> TestMempoolAcceptAsync(Transaction transaction, bool allowHighFees = false)
		{
			return await Rpc.TestMempoolAcceptAsync(transaction, allowHighFees);
		}

		public virtual async Task StopAsync()
		{
			await Rpc.StopAsync().ConfigureAwait(false);
		}

		public virtual async Task<uint256[]> GenerateAsync(int blockCount)
		{
			return await Rpc.GenerateAsync(blockCount).ConfigureAwait(false);
		}

		public virtual async Task<TimeSpan> UptimeAsync()
		{
			return await Rpc.UptimeAsync().ConfigureAwait(false);
		}

		public virtual async Task<uint256> SendRawTransactionAsync(Transaction transaction)
		{
			return await Rpc.SendRawTransactionAsync(transaction).ConfigureAwait(false);
		}

		public virtual async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative)
		{
			return await Rpc.EstimateSmartFeeAsync(confirmationTarget, estimateMode).ConfigureAwait(false);
		}

		public virtual IRPCClient PrepareBatch()
		{
			return new RpcClientBase(Rpc.PrepareBatch());
		}

		public async Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId)
		{
			var resp = await Rpc.SendCommandAsync(RPCOperations.getblock, blockId, 3).ConfigureAwait(false);
			return RpcParser.ParseVerboseBlockResponse(resp.Result.ToString());
		}

		#region For Testing Only

		public virtual async Task<uint256> SendToAddressAsync(BitcoinAddress address, Money amount, string commentTx = null, string commentDest = null, bool subtractFeeFromAmount = false, bool replaceable = false)
		{
			return await Rpc.SendToAddressAsync(address, amount, commentTx, commentDest, subtractFeeFromAmount, replaceable).ConfigureAwait(false);
		}

		public virtual async Task<uint256> GetBlockHashAsync(int height)
		{
			return await Rpc.GetBlockHashAsync(height).ConfigureAwait(false);
		}

		public virtual async Task InvalidateBlockAsync(uint256 blockHash)
		{
			await Rpc.InvalidateBlockAsync(blockHash).ConfigureAwait(false);
		}

		public virtual async Task AbandonTransactionAsync(uint256 txid)
		{
			await Rpc.AbandonTransactionAsync(txid).ConfigureAwait(false);
		}

		public virtual async Task<BumpResponse> BumpFeeAsync(uint256 txid)
		{
			return await Rpc.BumpFeeAsync(txid).ConfigureAwait(false);
		}

		public virtual async Task<Transaction> GetRawTransactionAsync(uint256 txid, bool throwIfNotFound = true)
		{
			return await Rpc.GetRawTransactionAsync(txid, throwIfNotFound).ConfigureAwait(false);
		}

		public virtual async Task<int> GetBlockCountAsync()
		{
			return await Rpc.GetBlockCountAsync().ConfigureAwait(false);
		}

		public virtual async Task<BitcoinAddress> GetNewAddressAsync()
		{
			return await Rpc.GetNewAddressAsync().ConfigureAwait(false);
		}

		public virtual async Task<GetTxOutResponse> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true)
		{
			return await Rpc.GetTxOutAsync(txid, index, includeMempool).ConfigureAwait(false);
		}

		public virtual async Task<SignRawTransactionResponse> SignRawTransactionWithWalletAsync(SignRawTransactionRequest request)
		{
			return await Rpc.SignRawTransactionWithWalletAsync(request).ConfigureAwait(false);
		}

		public virtual async Task<UnspentCoin[]> ListUnspentAsync()
		{
			return await Rpc.ListUnspentAsync().ConfigureAwait(false);
		}

		public virtual async Task SendBatchAsync()
		{
			await Rpc.SendBatchAsync().ConfigureAwait(false);
		}

		public Task<EstimateSmartFeeResponse> TryEstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative)
		{
			return Rpc.TryEstimateSmartFeeAsync(confirmationTarget, estimateMode: estimateMode);
		}

		#endregion For Testing Only
	}
}
