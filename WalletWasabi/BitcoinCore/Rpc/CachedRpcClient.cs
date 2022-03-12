using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NBitcoin;
using NBitcoin.RPC;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.BitcoinCore.Rpc;

public class CachedRpcClient : RpcClientBase
{
	private CancellationTokenSource _tipChangeCancellationTokenSource = new();

	public CachedRpcClient(RPCClient rpc, IMemoryCache cache)
		: base(rpc)
	{
		Cache = cache;
	}

	private object CancellationTokenSourceLock { get; } = new object();

	public IMemoryCache Cache { get; }

	private CancellationTokenSource TipChangeCancellationTokenSource
	{
		get
		{
			lock (CancellationTokenSourceLock)
			{
				if (_tipChangeCancellationTokenSource.IsCancellationRequested)
				{
					_tipChangeCancellationTokenSource.Dispose();
					_tipChangeCancellationTokenSource = new CancellationTokenSource();
				}
			}
			return _tipChangeCancellationTokenSource;
		}
	}

	public override async Task<uint256> GetBestBlockHashAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetBestBlockHashAsync);

		return await Cache.AtomicGetOrCreateAsync(
			cacheKey,
			CacheOptions(1, 4, true),
			() => base.GetBestBlockHashAsync(cancellationToken)).ConfigureAwait(false);
	}

	public override async Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetBlockAsync)}:{blockHash}";

		return await Cache.AtomicGetOrCreateAsync(
			cacheKey,
			CacheOptions(10, 4),
			() => base.GetBlockAsync(blockHash, cancellationToken)).ConfigureAwait(false);
	}

	public override async Task<Block> GetBlockAsync(uint blockHeight, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetBlockAsync)}:{blockHeight}";

		return await Cache.AtomicGetOrCreateAsync(
			cacheKey,
			CacheOptions(10, 4),
			() => base.GetBlockAsync(blockHeight, cancellationToken)).ConfigureAwait(false);
	}

	public override async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetBlockHeaderAsync)}:{blockHash}";

		return await Cache.AtomicGetOrCreateAsync(
			cacheKey,
			CacheOptions(2, 4),
			() => base.GetBlockHeaderAsync(blockHash, cancellationToken)).ConfigureAwait(false);
	}

	public override async Task<int> GetBlockCountAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetBlockCountAsync);

		return await Cache.AtomicGetOrCreateAsync(
			cacheKey,
			CacheOptions(1, 2, true),
			() => base.GetBlockCountAsync(cancellationToken)).ConfigureAwait(false);
	}

	public override async Task<PeerInfo[]> GetPeersInfoAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetPeersInfoAsync);

		return await Cache.AtomicGetOrCreateAsync(
			cacheKey,
			CacheOptions(2, 0.5),
			() => base.GetPeersInfoAsync(cancellationToken)).ConfigureAwait(false);
	}

	public override async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetMempoolEntryAsync)}:{txid}";

		return await Cache.AtomicGetOrCreateAsync(
			cacheKey,
			CacheOptions(20, 2, true),
			() => base.GetMempoolEntryAsync(txid, throwIfNotFound, cancellationToken)).ConfigureAwait(false);
	}

	public override async Task<MemPoolInfo> GetMempoolInfoAsync(CancellationToken cancel = default)
	{
		string cacheKey = nameof(GetMempoolInfoAsync);
		var cacheOptions = new MemoryCacheEntryOptions
		{
			Size = 1,
			AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
		};

		return await Cache.AtomicGetOrCreateAsync(
			cacheKey,
			CacheOptions(1, 10),
			() => base.GetMempoolInfoAsync(cancel)).ConfigureAwait(false);
	}

	public override async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(EstimateSmartFeeAsync)}:{confirmationTarget}:{estimateMode}";

		return await Cache.AtomicGetOrCreateAsync(
			cacheKey,
			CacheOptions(1, 4, true),
			() => base.EstimateSmartFeeAsync(confirmationTarget, estimateMode, cancellationToken)).ConfigureAwait(false);
	}

	public override async Task<uint256[]> GetRawMempoolAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetRawMempoolAsync);

		return await Cache.AtomicGetOrCreateAsync(
			cacheKey,
			CacheOptions(20, 2, true),
			() => base.GetRawMempoolAsync(cancellationToken)).ConfigureAwait(false);
	}

	public override async Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetTxOutAsync)}:{txid}:{index}:{includeMempool}";

		return await Cache.AtomicGetOrCreateAsync(
			cacheKey,
			CacheOptions(2, 2, true),
			() => base.GetTxOutAsync(txid, index, includeMempool, cancellationToken)).ConfigureAwait(false);
	}

	public override async Task<uint256[]> GenerateAsync(int blockCount, CancellationToken cancellationToken = default)
	{
		TipChangeCancellationTokenSource.Cancel();
		return await base.GenerateAsync(blockCount, cancellationToken).ConfigureAwait(false);
	}

	public override async Task InvalidateBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		TipChangeCancellationTokenSource.Cancel();
		await base.InvalidateBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
	}

	private MemoryCacheEntryOptions CacheOptions(int size, double expireInSeconds, bool addExpitationToken = false)
	{
		var cacheOptions = new MemoryCacheEntryOptions
		{
			Size = size,
			AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(expireInSeconds)
		};
		if (addExpitationToken)
		{
			cacheOptions.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));
		}
		return cacheOptions;
	}
}
