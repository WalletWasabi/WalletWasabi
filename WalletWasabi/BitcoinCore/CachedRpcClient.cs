using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.BitcoinCore
{
	public class CachedRpcClient : RpcClientBase
	{
		private static Dictionary<string, SemaphoreSlim> Semaphores = new Dictionary<string, SemaphoreSlim>
		{
			{ nameof(GetBestBlockHashAsync), new SemaphoreSlim(1) },
			{ nameof(GetBlockAsync) + "height", new SemaphoreSlim(1) },
			{ nameof(GetBlockAsync) + "hash", new SemaphoreSlim(1) },
			{ nameof(GetBlockHeaderAsync), new SemaphoreSlim(1) },
			{ nameof(GetBlockCountAsync), new SemaphoreSlim(1) },
			{ nameof(GetPeersInfoAsync), new SemaphoreSlim(1) },
			{ nameof(GetMempoolEntryAsync), new SemaphoreSlim(1) },
			{ nameof(GetRawMempoolAsync), new SemaphoreSlim(1) },
		};

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

		public override async Task<uint256> GetBestBlockHashAsync() =>
			await Get<uint256>(
				nameof(GetBestBlockHashAsync),
				() => base.GetBestBlockHashAsync(),
				new MemoryCacheEntryOptions()
					.SetSize(1)
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4)) // The best hash doesn't change so often so, keep in cache for 4 seconds.
					.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token)));

		public override async Task<Block> GetBlockAsync(uint256 blockHash) =>
			await Get<Block>(
				$"{nameof(GetBlockAsync)}hash:{blockHash}",
				() => base.GetBlockAsync(blockHash),
				new MemoryCacheEntryOptions()
					.SetSize(10)
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4))); // There is a block every 10 minutes in average so, keep in cache for 4 seconds.

		public override async Task<Block> GetBlockAsync(uint blockHeight) =>
			await Get<Block>(
				$"{nameof(GetBlockAsync)}height:{blockHeight}",
				() => base.GetBlockAsync(blockHeight),
				new MemoryCacheEntryOptions()
					.SetSize(10)
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4))); // There is a block every 10 minutes in average so, keep in cache for 4 seconds.

		public override async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash) =>
			await Get<BlockHeader>(
				$"{nameof(GetBlockHeaderAsync)}:{blockHash}",
				() => base.GetBlockHeaderAsync(blockHash),
				new MemoryCacheEntryOptions()
					.SetSize(2)
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4))); // There is a block every 10 minutes in average so, keep in cache for 4 seconds.

		public override async Task<int> GetBlockCountAsync() =>
			await Get<int>(
				nameof(GetBlockCountAsync),
				() => base.GetBlockCountAsync(),
				new MemoryCacheEntryOptions()
					.SetSize(1)
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2)) // The blockchain info does not change frequently.
					.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token)));

		public override async Task<PeerInfo[]> GetPeersInfoAsync() =>
			await Get<PeerInfo[]>(
				nameof(GetPeersInfoAsync),
				() => base.GetPeersInfoAsync(),
				new MemoryCacheEntryOptions()
					.SetSize(2)
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(0.5)));

		public override async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true) =>
			await Get<MempoolEntry>(
				$"{nameof(GetMempoolEntryAsync)}:{txid}",
				() => base.GetMempoolEntryAsync(txid, throwIfNotFound),
				new MemoryCacheEntryOptions()
					.SetSize(20)
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2))
					.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token)));

		public override async Task<uint256[]> GetRawMempoolAsync() =>
			await Get<uint256[]>(
				nameof(GetRawMempoolAsync),
				() => base.GetRawMempoolAsync(),
				new MemoryCacheEntryOptions()
					.SetSize(20)
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2))
					.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token)));

		private static object GetTxOutLock = new object();

		public override GetTxOutResponse GetTxOut(uint256 txid, int index, bool includeMempool = true)
		{
			string cacheKey = $"{nameof(GetTxOut)}:{txid}:{index}:{includeMempool}";

			if (Cache.TryGetValue(cacheKey, out GetTxOutResponse txout))
			{
				return txout;
			}

			lock (GetTxOutLock)
			{
				if (!Cache.TryGetValue(cacheKey, out txout))
				{
					txout = base.GetTxOut(txid, index, includeMempool);

					var cacheEntryOptions = new MemoryCacheEntryOptions()
						.SetSize(2)
						.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

					// Save data in cache.
					Cache.Set(cacheKey, txout, cacheEntryOptions);
				}
				return txout;
			}
		}

		public override async Task<uint256[]> GenerateAsync(int blockCount)
		{
			TipChangeCancellationTokenSource.Cancel();
			return await base.GenerateAsync(blockCount).ConfigureAwait(false);
		}

		public override async Task InvalidateBlockAsync(uint256 blockHash)
		{
			TipChangeCancellationTokenSource.Cancel();
			await base.InvalidateBlockAsync(blockHash);
		}

		private async Task<T> Get<T>(string cacheKey, Func<Task<T>> fetch, MemoryCacheEntryOptions cacheEntryOptions)
		{
			if (Cache.TryGetValue(cacheKey, out T cachedObject))
			{
				return cachedObject;
			}

			var separatorIndex = cacheKey.IndexOf(':');
			var semaphoreName = separatorIndex >= 0
				? cacheKey[..separatorIndex]
				: cacheKey;

			var semaphore = Semaphores[semaphoreName];
			await semaphore.WaitAsync().ConfigureAwait(false);
			try
			{
				if (!Cache.TryGetValue(cacheKey, out cachedObject))
				{
					cachedObject = await fetch().ConfigureAwait(false);
					Cache.Set(cacheKey, cachedObject, cacheEntryOptions);
				}
				return cachedObject;
			}
			finally
			{
				semaphore.Release();
			}
		}
	}
}
