using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc.Models;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Backend.Statistics;

namespace WalletWasabi.BitcoinCore.Rpc;

public class RpcClientBase : IRPCClient
{
	public RpcClientBase(RPCClient rpc)
	{
		Rpc = rpc;
	}

	public Network Network => Rpc.Network;

	protected internal RPCClient Rpc { get; }

	public RPCCredentialString CredentialString => Rpc.CredentialString;

	public virtual async Task<uint256> GetBestBlockHashAsync(CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(GetBestBlockHashAsync),
			() => Rpc.GetBestBlockHashAsync(cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(GetBlockAsync),
			() => Rpc.GetBlockAsync(blockHash, cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<Block> GetBlockAsync(uint blockHeight, CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(GetBlockAsync),
			() => Rpc.GetBlockAsync(blockHeight, cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(GetBlockHeaderAsync),
			() => Rpc.GetBlockHeaderAsync(blockHash, cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<BlockchainInfo> GetBlockchainInfoAsync(CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(GetBlockchainInfoAsync),
			() => Rpc.GetBlockchainInfoAsync(cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<PeerInfo[]> GetPeersInfoAsync(CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(GetPeersInfoAsync),
			() => Rpc.GetPeersInfoAsync(cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(GetMempoolEntryAsync),
			() => Rpc.GetMempoolEntryAsync(txid, throwIfNotFound, cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<MemPoolInfo> GetMempoolInfoAsync(CancellationToken cancel = default)
	{
		return await Measure(
			nameof(GetMempoolInfoAsync),
			async () =>
			{
				try
				{
					var response = await Rpc.SendCommandAsync(RPCOperations.getmempoolinfo, cancel, true)
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
							_ => Enumerable.Empty<FeeRateGroup>()
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
					cancel.ThrowIfCancellationRequested();

					return await Rpc.GetMemPoolAsync(cancel).ConfigureAwait(false);
				}
			}).ConfigureAwait(false);
	}

	public virtual async Task<uint256[]> GetRawMempoolAsync(CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(GetRawMempoolAsync),
			() => Rpc.GetRawMempoolAsync(cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true, CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(GetTxOutAsync),
			() => Rpc.GetTxOutAsync(txid, index, includeMempool, cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<MempoolAcceptResult> TestMempoolAcceptAsync(Transaction transaction, CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(TestMempoolAcceptAsync),
			() => Rpc.TestMempoolAcceptAsync(transaction, cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task StopAsync(CancellationToken cancellationToken = default)
	{
		await Rpc.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<uint256[]> GenerateAsync(int blockCount, CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(GenerateAsync),
			() => Rpc.GenerateAsync(blockCount, cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<TimeSpan> UptimeAsync(CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(UptimeAsync),
			() => Rpc.UptimeAsync(cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<uint256> SendRawTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(SendRawTransactionAsync),
			() => Rpc.SendRawTransactionAsync(transaction, cancellationToken)).ConfigureAwait(false);
	}

	public virtual async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(EstimateSmartFeeAsync),
			() => Rpc.EstimateSmartFeeAsync(confirmationTarget, estimateMode, cancellationToken)).ConfigureAwait(false);
	}

	public virtual IRPCClient PrepareBatch()
	{
		return new RpcClientBase(Rpc.PrepareBatch());
	}

	public virtual async Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId, CancellationToken cancellationToken = default)
	{
		var resp = await Measure(
			nameof(GetVerboseBlockAsync),
			() => Rpc.SendCommandAsync(RPCOperations.getblock, cancellationToken, blockId, 3)).ConfigureAwait(false);
		return RpcParser.ParseVerboseBlockResponse(resp.ResultString);
	}

	public async Task<uint256[]> GenerateToAddressAsync(int nBlocks, BitcoinAddress address, CancellationToken cancellationToken = default)
	{
		return await Measure(
			nameof(GenerateToAddressAsync),
			() => Rpc.GenerateToAddressAsync(nBlocks, address, cancellationToken)).ConfigureAwait(false);
	}

	#region For Testing Only

	public virtual async Task<uint256> SendToAddressAsync(BitcoinAddress address, Money amount, bool replaceable = false, CancellationToken cancellationToken = default)
	{
		var parameters = new SendToAddressParameters { Replaceable = replaceable };
		return await Rpc.SendToAddressAsync(address, amount, parameters, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<uint256> GetBlockHashAsync(int height, CancellationToken cancellationToken = default)
	{
		return await Rpc.GetBlockHashAsync(height, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task InvalidateBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		await Rpc.InvalidateBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task AbandonTransactionAsync(uint256 txid /*, CancellationToken cancellationToken = default*/)
	{
		await Rpc.AbandonTransactionAsync(txid /*, cancellationToken*/).ConfigureAwait(false);
	}

	public virtual async Task<BumpResponse> BumpFeeAsync(uint256 txid, CancellationToken cancellationToken = default)
	{
		return await Rpc.BumpFeeAsync(txid, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<Transaction> GetRawTransactionAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default)
	{
		return await Rpc.GetRawTransactionAsync(txid, throwIfNotFound, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<IEnumerable<Transaction>> GetRawTransactionsAsync(IEnumerable<uint256> txids, CancellationToken cancel)
	{
		// 8 is half of the default rpcworkqueue
		List<Transaction> acquiredTransactions = new();
		foreach (var txidsChunk in txids.ChunkBy(8))
		{
			IRPCClient batchingRpc = PrepareBatch();
			List<Task<Transaction>> tasks = new();
			foreach (var txid in txidsChunk)
			{
				tasks.Add(batchingRpc.GetRawTransactionAsync(txid, throwIfNotFound: false, cancel));
			}

			await batchingRpc.SendBatchAsync(cancel).ConfigureAwait(false);

			foreach (var tx in await Task.WhenAll(tasks).ConfigureAwait(false))
			{
				if (tx is not null)
				{
					acquiredTransactions.Add(tx);
				}
				cancel.ThrowIfCancellationRequested();
			}
		}

		return acquiredTransactions;
	}

	public virtual async Task<int> GetBlockCountAsync(CancellationToken cancellationToken = default)
	{
		return await Rpc.GetBlockCountAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<BitcoinAddress> GetNewAddressAsync(CancellationToken cancellationToken = default)
	{
		return await Rpc.GetNewAddressAsync(cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<SignRawTransactionResponse> SignRawTransactionWithWalletAsync(SignRawTransactionRequest request, CancellationToken cancellationToken = default)
	{
		return await Rpc.SignRawTransactionWithWalletAsync(request, cancellationToken).ConfigureAwait(false);
	}

	public virtual async Task<UnspentCoin[]> ListUnspentAsync(/*CancellationToken cancellationToken = default*/)
	{
		return await Rpc.ListUnspentAsync(/*cancellationToken*/).ConfigureAwait(false);
	}

	public virtual async Task SendBatchAsync(CancellationToken cancellationToken = default)
	{
		await Rpc.SendBatchAsync(cancellationToken).ConfigureAwait(false);
	}

	public Task<EstimateSmartFeeResponse> TryEstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, CancellationToken cancellationToken = default)
	{
		return Rpc.TryEstimateSmartFeeAsync(confirmationTarget, estimateMode: estimateMode, cancellationToken);
	}

	public Task<RPCClient> CreateWalletAsync(string walletNameOrPath, CreateWalletOptions? options = null, CancellationToken cancellationToken = default)
	{
		return Rpc.CreateWalletAsync(walletNameOrPath, options, cancellationToken);
	}

	#endregion For Testing Only

	private async Task<T> Measure<T>(string methodName, Func<Task<T>> fnc)
	{
		var start = DateTimeOffset.UtcNow;
		try
		{
			return await fnc().ConfigureAwait(false);
		}
		finally
		{
			RequestTimeStatista.Instance.Add(methodName, DateTimeOffset.UtcNow - start);
		}
	}

}
