using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NBitcoin;
using NBitcoin.RPC;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc.Models;
using WalletWasabi.Cache;
using static WalletWasabi.Cache.IdempotencyRequestCache;

namespace WalletWasabi.BitcoinCore.Rpc;

public class CachedRpcClient : RpcClientBase
{
	private CancellationTokenSource _tipChangeCancellationTokenSource = new();

	public CachedRpcClient(RPCClient rpc, IMemoryCache cache)
		: base(rpc)
	{
		Cache = cache;
		IdempotencyRequestCache = new(cache);
	}

	private object CancellationTokenSourceLock { get; } = new();

	public IMemoryCache Cache { get; }

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
			options: CacheOptions(size: 1, expireInSeconds: 4, addExpirationToken: true),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetBlockAsync)}:{blockHash}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetBlockAsync(blockHash, cancellationToken),
			options: CacheOptions(size: 10, expireInSeconds: 4),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<Block> GetBlockAsync(uint blockHeight, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetBlockAsync)}:{blockHeight}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetBlockAsync(blockHeight, cancellationToken),
			options: CacheOptions(size: 10, expireInSeconds: 4),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<VerboseBlockInfo> GetVerboseBlockAsync(uint256 blockId, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetVerboseBlockAsync)}:{blockId}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetVerboseBlockAsync(blockId, cancellationToken),
			options: CacheOptions(size: 20, expireInSeconds: 4),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetBlockHeaderAsync)}:{blockHash}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetBlockHeaderAsync(blockHash, cancellationToken),
			options: CacheOptions(size: 2, expireInSeconds: 4),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<int> GetBlockCountAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetBlockCountAsync);

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetBlockCountAsync(cancellationToken),
			options: CacheOptions(size: 1, expireInSeconds: 2),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<PeerInfo[]> GetPeersInfoAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetPeersInfoAsync);

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetPeersInfoAsync(cancellationToken),
			options: CacheOptions(size: 2, expireInSeconds: 0.5),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetMempoolEntryAsync)}:{txid}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetMempoolEntryAsync(txid, throwIfNotFound, cancellationToken),
			options: CacheOptions(size: 20, expireInSeconds: 2, addExpirationToken: true),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<MemPoolInfo> GetMempoolInfoAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetMempoolInfoAsync);

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetMempoolInfoAsync(cancellationToken),
			options: CacheOptions(size: 1, expireInSeconds: 10),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(EstimateSmartFeeAsync)}:{confirmationTarget}:{estimateMode}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.EstimateSmartFeeAsync(confirmationTarget, estimateMode, cancellationToken),
			options: CacheOptions(size: 1, expireInSeconds: 10, addExpirationToken: true),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<uint256[]> GetRawMempoolAsync(CancellationToken cancellationToken = default)
	{
		string cacheKey = nameof(GetRawMempoolAsync);

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetRawMempoolAsync(cancellationToken),
			options: CacheOptions(size: 20, expireInSeconds: 2, addExpirationToken: true),
			cancellationToken).ConfigureAwait(false);
	}

	public override async Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true, CancellationToken cancellationToken = default)
	{
		string cacheKey = $"{nameof(GetTxOutAsync)}:{txid}:{index}:{includeMempool}";

		return await IdempotencyRequestCache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken cancellationToken) => base.GetTxOutAsync(txid, index, includeMempool, cancellationToken),
			options: CacheOptions(size: 2, expireInSeconds: 2, addExpirationToken: true),
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

	public Task<TResult> GetDataAsync<TRequest, TResult>(
		TRequest request,
		ProcessRequestDelegateAsync<TRequest, TResult> action,
		MemoryCacheEntryOptions memoryCacheEntryOptions,
		CancellationToken cancellationToken)
		where TRequest : notnull
	{
		return IdempotencyRequestCache.GetCachedResponseAsync(request, action, memoryCacheEntryOptions, cancellationToken);
	}

	private MemoryCacheEntryOptions CacheOptions(int size, double expireInSeconds, bool addExpirationToken = false)
	{
		var cacheOptions = new MemoryCacheEntryOptions
		{
			Size = size,
			AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(expireInSeconds)
		};
		if (addExpirationToken)
		{
			cacheOptions.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));
		}
		return cacheOptions;
	}
}
