using System.Globalization;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using WalletWasabi.BitcoinRpc.Models;

namespace WalletWasabi.BitcoinRpc;

public class RpcClientBase : IRPCClient
{
	public RpcClientBase(RPCClient rpcClient)
	{
		RpcClient = rpcClient;
	}

	public Network Network => RpcClient.Network;

	protected internal RPCClient RpcClient { get; }

	public RPCCredentialString CredentialString => RpcClient.CredentialString;

	public virtual async Task<uint256> GetBestBlockHashAsync(CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetBestBlockHashAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<Block> GetBlockAsync(uint blockHeight, CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetBlockAsync(blockHeight, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetBlockHeaderAsync(blockHash, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<BlockchainInfo> GetBlockchainInfoAsync(CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetBlockchainInfoAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<PeerInfo[]> GetPeersInfoAsync(CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetPeersInfoAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetMempoolEntryAsync(txid, throwIfNotFound, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<MemPoolInfo> GetMempoolInfoAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await RpcClient.SendCommandAsync(RPCOperations.getmempoolinfo, cancellationToken, true)
				.ConfigureAwait(false);

			static IEnumerable<FeeRateGroup> ExtractFeeRateGroups(JToken jt) =>
				jt switch
				{
					JObject jo => jo.Properties()
						.Where(p => p.Name != "total_fees")
						.Select(
							p => new FeeRateGroup
							{
								Group = int.Parse(p.Name),
								Sizes = p.Value.Value<ulong>("sizes"),
								Count = p.Value.Value<uint>("count"),
								Fees = Money.Satoshis(p.Value.Value<ulong>("fees")),
								From = new FeeRate(p.Value.Value<decimal>("from_feerate")),
								To = new FeeRate(Math.Min(50_000, p.Value.Value<decimal>("to_feerate")))
							}),
					_ => []
				};

			return new MemPoolInfo()
			{
				Size = int.Parse((string) response.Result["size"]!, CultureInfo.InvariantCulture),
				Bytes = int.Parse((string) response.Result["bytes"]!, CultureInfo.InvariantCulture),
				Usage = int.Parse((string) response.Result["usage"]!, CultureInfo.InvariantCulture),
				MaxMemPool =
					double.Parse((string) response.Result["maxmempool"]!, CultureInfo.InvariantCulture),
				MemPoolMinFee = double.Parse(
					(string) response.Result["mempoolminfee"]!,
					CultureInfo.InvariantCulture),
				MinRelayTxFee = double.Parse(
					(string) response.Result["minrelaytxfee"]!,
					CultureInfo.InvariantCulture),
				Histogram = ExtractFeeRateGroups(response.Result["fee_histogram"]!).ToArray()
			};
		}
		catch (RPCException ex) when (ex.RPCCode == RPCErrorCode.RPC_MISC_ERROR)
		{
			cancellationToken.ThrowIfCancellationRequested();

			return await RpcClient.GetMemPoolAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	public virtual async Task<uint256[]> GetRawMempoolAsync(CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetRawMempoolAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true, CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetTxOutAsync(txid, index, includeMempool, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<MempoolAcceptResult> TestMempoolAcceptAsync(Transaction transaction, CancellationToken cancellationToken = default)
	{
		return await RpcClient.TestMempoolAcceptAsync(transaction, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<ScanTxoutSetResponse> StartScanTxoutSetAsync(ScanTxoutSetParameters parameters, CancellationToken cancellationToken = default)
	{
		return await RpcClient.StartScanTxoutSetAsync(parameters, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task StopAsync(CancellationToken cancellationToken = default)
	{
		await RpcClient.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<uint256[]> GenerateAsync(int blockCount, CancellationToken cancellationToken = default)
	{
		return await RpcClient.GenerateAsync(blockCount, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<TimeSpan> UptimeAsync(CancellationToken cancellationToken = default)
	{
		return await RpcClient.UptimeAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<uint256> SendRawTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
	{
		return await RpcClient.SendRawTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, CancellationToken cancellationToken = default)
	{
		return await RpcClient.EstimateSmartFeeAsync(confirmationTarget, estimateMode, cancellationToken).ConfigureAwait(false);
	}

	public virtual IRPCClient PrepareBatch()
	{
		return new RpcClientBase(RpcClient.PrepareBatch());
	}

	public virtual async Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId, CancellationToken cancellationToken = default)
	{
		var resp = await RpcClient.SendCommandAsync(RPCOperations.getblock, cancellationToken, blockId, 3).ConfigureAwait(false);
		return RpcParser.ParseVerboseBlockResponse(resp.ResultString);
	}

	public virtual async Task<BlockFilter> GetBlockFilterAsync(uint256 blockId, CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetBlockFilterAsync(blockId, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<uint256[]> GenerateToAddressAsync(int nBlocks, BitcoinAddress address, CancellationToken cancellationToken = default)
	{
		return await RpcClient.GenerateToAddressAsync(nBlocks, address, cancellationToken).ConfigureAwait(false);
	}

	#region For Testing Only

	public virtual async Task<uint256> SendToAddressAsync(BitcoinAddress address, Money amount, bool replaceable = false, CancellationToken cancellationToken = default)
	{
		var parameters = new SendToAddressParameters { Replaceable = replaceable };
		return await RpcClient.SendToAddressAsync(address, amount, parameters, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<uint256> GetBlockHashAsync(int height, CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetBlockHashAsync(height, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task InvalidateBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		await RpcClient.InvalidateBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task AbandonTransactionAsync(uint256 txid /*, CancellationToken cancellationToken = default*/)
	{
		await RpcClient.AbandonTransactionAsync(txid /*, cancellationToken*/).ConfigureAwait(false);
	}

	public virtual async Task<BumpResponse> BumpFeeAsync(uint256 txid, CancellationToken cancellationToken = default)
	{
		return await RpcClient.BumpFeeAsync(txid, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<Transaction> GetRawTransactionAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetRawTransactionAsync(txid, throwIfNotFound, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<IEnumerable<Transaction>> GetRawTransactionsAsync(IEnumerable<uint256> txids, CancellationToken cancellationToken)
	{
		// 8 is half of the default rpcworkqueue
		List<Transaction> acquiredTransactions = new();
		foreach (var txidsChunk in txids.ChunkBy(8))
		{
			IRPCClient batchingRpc = PrepareBatch();
			List<Task<Transaction>> tasks = new();
			foreach (var txid in txidsChunk)
			{
				tasks.Add(batchingRpc.GetRawTransactionAsync(txid, throwIfNotFound: false, cancellationToken));
			}

			await batchingRpc.SendBatchAsync(cancellationToken).ConfigureAwait(false);

			foreach (var tx in await Task.WhenAll(tasks).ConfigureAwait(false))
			{
				if (tx is not null)
				{
					acquiredTransactions.Add(tx);
				}
				cancellationToken.ThrowIfCancellationRequested();
			}
		}

		return acquiredTransactions;
	}

	public virtual async Task<int> GetBlockCountAsync(CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetBlockCountAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<BitcoinAddress> GetNewAddressAsync(CancellationToken cancellationToken = default)
	{
		return await RpcClient.GetNewAddressAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<SignRawTransactionResponse> SignRawTransactionWithWalletAsync(SignRawTransactionRequest request, CancellationToken cancellationToken = default)
	{
		return await RpcClient.SignRawTransactionWithWalletAsync(request, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<UnspentCoin[]> ListUnspentAsync(/*CancellationToken cancellationToken = default*/)
	{
		return await RpcClient.ListUnspentAsync(/*cancellationToken*/).ConfigureAwait(false);
	}

	public virtual async Task SendBatchAsync(CancellationToken cancellationToken = default)
	{
		await RpcClient.SendBatchAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual Task<RPCClient> CreateWalletAsync(string walletNameOrPath, CreateWalletOptions? options = null, CancellationToken cancellationToken = default)
	{
		return RpcClient.CreateWalletAsync(walletNameOrPath, options, cancellationToken);
	}
	#endregion For Testing Only
}
