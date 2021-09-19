using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.BitcoinCore.Rpc
{
	public class CachedRpcClient : RpcClientBase
	{
		private CancellationTokenSource _tipChangeCancellationTokenSource;

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
					if (_tipChangeCancellationTokenSource is null || _tipChangeCancellationTokenSource.IsCancellationRequested)
					{
						_tipChangeCancellationTokenSource = new CancellationTokenSource();
					}
				}
				return _tipChangeCancellationTokenSource;
			}
		}

		public override async Task<uint256> GetBestBlockHashAsync(CancellationToken cancellationToken = default)
		{
			string cacheKey = nameof(GetBestBlockHashAsync);
			var cacheOptions = new MemoryCacheEntryOptions
			{
				Size = 1,
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(4) // The best hash doesn't change so often so, keep in cache for 4 seconds.
			};
			cacheOptions.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				cacheOptions,
				() => base.GetBestBlockHashAsync(cancellationToken)).ConfigureAwait(false);
		}

		public override async Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancellationToken = default)
		{
			string cacheKey = $"{nameof(GetBlockAsync)}:{blockHash}";
			var cacheOptions = new MemoryCacheEntryOptions
			{
				Size = 10,
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(4) // There is a block every 10 minutes on average so, keep in cache for 4 seconds.
			};

			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				cacheOptions,
				() => base.GetBlockAsync(blockHash, cancellationToken)).ConfigureAwait(false);
		}

		public override async Task<Block> GetBlockAsync(uint blockHeight, CancellationToken cancellationToken = default)
		{
			string cacheKey = $"{nameof(GetBlockAsync)}:{blockHeight}";
			var cacheOptions = new MemoryCacheEntryOptions
			{
				Size = 10,
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(4) // There is a block every 10 minutes on average so, keep in cache for 4 seconds.
			};

			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				cacheOptions,
				() => base.GetBlockAsync(blockHeight, cancellationToken)).ConfigureAwait(false);
		}

		public override async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash, CancellationToken cancellationToken = default)
		{
			string cacheKey = $"{nameof(GetBlockHeaderAsync)}:{blockHash}";
			var cacheOptions = new MemoryCacheEntryOptions
			{
				Size = 2,
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(4) // There is a block every 10 minutes on average so, keep in cache for 4 seconds.
			};

			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				cacheOptions,
				() => base.GetBlockHeaderAsync(blockHash, cancellationToken)).ConfigureAwait(false);
		}

		public override async Task<int> GetBlockCountAsync(CancellationToken cancellationToken = default)
		{
			string cacheKey = nameof(GetBlockCountAsync);
			var cacheOptions = new MemoryCacheEntryOptions
			{
				Size = 1,
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2) // The blockchain info does not change frequently.
			};
			cacheOptions.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				cacheOptions,
				() => base.GetBlockCountAsync(cancellationToken)).ConfigureAwait(false);
		}

		public override async Task<PeerInfo[]> GetPeersInfoAsync(CancellationToken cancellationToken = default)
		{
			string cacheKey = nameof(GetPeersInfoAsync);
			var cacheOptions = new MemoryCacheEntryOptions
			{
				Size = 2,
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(0.5)
			};

			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				cacheOptions,
				() => base.GetPeersInfoAsync(cancellationToken)).ConfigureAwait(false);
		}

		public override async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true, CancellationToken cancellationToken = default)
		{
			string cacheKey = $"{nameof(GetMempoolEntryAsync)}:{txid}";
			var cacheOptions = new MemoryCacheEntryOptions
			{
				Size = 20,
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2)
			};
			cacheOptions.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				cacheOptions,
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
				cacheOptions,
				() => base.GetMempoolInfoAsync(cancel)).ConfigureAwait(false);
		}

		public override async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, CancellationToken cancellationToken = default)
		{
			string cacheKey = $"{nameof(EstimateSmartFeeAsync)}:{confirmationTarget}:{estimateMode}";
			var cacheOptions = new MemoryCacheEntryOptions
			{
				Size = 1,
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(4)
			};
			cacheOptions.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				cacheOptions,
				() => base.EstimateSmartFeeAsync(confirmationTarget, estimateMode, cancellationToken)).ConfigureAwait(false);
		}

		public override async Task<uint256[]> GetRawMempoolAsync(CancellationToken cancellationToken = default)
		{
			string cacheKey = nameof(GetRawMempoolAsync);
			var cacheOptions = new MemoryCacheEntryOptions
			{
				Size = 20,
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2)
			};
			cacheOptions.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				cacheOptions,
				() => base.GetRawMempoolAsync(cancellationToken)).ConfigureAwait(false);
		}

		public override async Task<GetTxOutResponse?> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true, CancellationToken cancellationToken = default)
		{
			string cacheKey = $"{nameof(GetTxOutAsync)}:{txid}:{index}:{includeMempool}";
			var cacheOptions = new MemoryCacheEntryOptions
			{
				Size = 2,
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2)
			};

			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				cacheOptions,
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
	}
}
