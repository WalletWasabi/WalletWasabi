using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NBitcoin;
using NBitcoin.RPC;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc.Models;
using WalletWasabi.Cache;

namespace WalletWasabi.BitcoinCore.Rpc;

public class CachedRpcClient : RpcClientBase
{
	private CancellationTokenSource _tipChangeCancellationTokenSource = new();

	public CachedRpcClient(RPCClient rpc, IMemoryCache cache)
		: base(rpc)
	{
		IdempotencyRequestCache = new(cache);
	}

	private static MemoryCacheEntryOptions GetBlockCacheOptions { get; } = CacheOptions(size: 10, expireInSeconds: 300);
	private static MemoryCacheEntryOptions GetVerboseBlockCacheOptions { get; } = CacheOptions(size: 20, expireInSeconds: 300);
	private static MemoryCacheEntryOptions GetBlockHeaderCacheOptions { get; } = CacheOptions(size: 2, expireInSeconds: 300);
	private static MemoryCacheEntryOptions GetPeersInfoCacheOptions { get; } = CacheOptions(size: 2, expireInSeconds: 0.5);
	private static MemoryCacheEntryOptions GetMempoolInfoCacheOptions { get; } = CacheOptions(size: 1, expireInSeconds: 10);

	private object CancellationTokenSourceLock { get; } = new();

	private IdempotencyRequestCache IdempotencyRequestCache { get; }

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

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetBestBlockHashAsync(cancellationToken),
			options: CacheOptionsWithExpirationToken(size: 1, expireInSeconds: 4),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetBlockAsync)}:{blockHash}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetBlockAsync(blockHash, cancellationToken),
			options: GetBlockCacheOptions,
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<Block> GetBlockAsync(uint blockHeight, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetBlockAsync)}:{blockHeight}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetBlockAsync(blockHeight, cancellationToken),
			options: GetBlockCacheOptions,
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetVerboseBlockAsync)}:{blockId}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetVerboseBlockAsync(blockId, cancellationToken),
			options: GetVerboseBlockCacheOptions,
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetBlockHeaderAsync)}:{blockHash}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetBlockHeaderAsync(blockHash, cancellationToken),
			options: GetBlockHeaderCacheOptions,
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<int> GetBlockCountAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetBlockCountAsync);

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetBlockCountAsync(cancellationToken),
			options: CacheOptionsWithExpirationToken(size: 1, expireInSeconds: 2),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<PeerInfo[]> GetPeersInfoAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetPeersInfoAsync);

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetPeersInfoAsync(cancellationToken),
			options: GetPeersInfoCacheOptions,
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetMempoolEntryAsync)}:{txid}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetMempoolEntryAsync(txid, throwIfNotFound, cancellationToken),
			options: CacheOptionsWithExpirationToken(size: 20, expireInSeconds: 2),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<MemPoolInfo> GetMempoolInfoAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetMempoolInfoAsync);

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetMempoolInfoAsync(cancellationToken),
			options: GetMempoolInfoCacheOptions,
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(EstimateSmartFeeAsync)}:{confirmationTarget}:{estimateMode}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.EstimateSmartFeeAsync(confirmationTarget, estimateMode, cancellationToken),
			options: CacheOptionsWithExpirationToken(size: 1, expireInSeconds: 60),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<uint256[]> GetRawMempoolAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetRawMempoolAsync);

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetRawMempoolAsync(cancellationToken),
			options: CacheOptionsWithExpirationToken(size: 20, expireInSeconds: 2),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetTxOutAsync)}:{txid}:{index}:{includeMempool}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetTxOutAsync(txid, index, includeMempool, cancellationToken),
			options: CacheOptionsWithExpirationToken(size: 2, expireInSeconds: 2),
			cancellationToken).ConfigureAwait(false);
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

	private MemoryCacheEntryOptions CacheOptionsWithExpirationToken(int size, double expireInSeconds)
	{
		MemoryCacheEntryOptions cacheOptions = CacheOptions(size, expireInSeconds);
		cacheOptions.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

		return cacheOptions;
	}

	private static MemoryCacheEntryOptions CacheOptions(int size, double expireInSeconds)
	{
		var cacheOptions = new MemoryCacheEntryOptions
		{
			Size = size,
			AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(expireInSeconds)
		};

		return cacheOptions;
	}
}
