using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.BitcoinCore
{
	public class CachedRpcClient : RpcClientBase
	{
		private CancellationTokenSource _tipChangeCancellationTokenSource;
		private object CancellationTokenSourceLock { get; } = new object();

		public CachedRpcClient(RPCClient rpc, IMemoryCache cache)
			: base(rpc)
		{
			Cache = cache;
		}

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

		public override async Task<uint256> GetBestBlockHashAsync()
		{
			string cacheKey = nameof(GetBestBlockHashAsync);
			if (!Cache.TryGetValue(cacheKey, out uint256 blockHash))
			{
				blockHash = await base.GetBestBlockHashAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetSize(1)
					// The best hash doesn't change so often so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4))
					.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

				// Save data in cache.
				Cache.Set(cacheKey, blockHash, cacheEntryOptions);
			}
			return blockHash;
		}

		public override async Task<Block> GetBlockAsync(uint256 blockHash)
		{
			string cacheKey = $"{nameof(GetBlockAsync)}:{blockHash}";
			if (!Cache.TryGetValue(cacheKey, out Block block))
			{
				block = await base.GetBlockAsync(blockHash).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetSize(10)
					// There is a block every 10 minutes in average so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

				// Save data in cache.
				Cache.Set(cacheKey, block, cacheEntryOptions);
			}
			return block;
		}

		public override async Task<Block> GetBlockAsync(uint blockHeight)
		{
			string cacheKey = $"{nameof(GetBlockAsync)}:{blockHeight}";
			if (!Cache.TryGetValue(cacheKey, out Block block))
			{
				block = await base.GetBlockAsync(blockHeight).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetSize(10)
					// There is a block every 10 minutes in average so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

				// Save data in cache.
				Cache.Set(cacheKey, block, cacheEntryOptions);
			}
			return block;
		}

		public override async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash)
		{
			string cacheKey = $"{nameof(GetBlockHeaderAsync)}:{blockHash}";
			if (!Cache.TryGetValue(cacheKey, out BlockHeader blockHeader))
			{
				blockHeader = await base.GetBlockHeaderAsync(blockHash).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetSize(2)
					// There is a block every 10 minutes in average so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

				// Save data in cache.
				Cache.Set(cacheKey, blockHeader, cacheEntryOptions);
			}
			return blockHeader;
		}

		public override async Task<int> GetBlockCountAsync()
		{
			string cacheKey = nameof(GetBlockCountAsync);
			if (!Cache.TryGetValue(cacheKey, out int blockCount))
			{
				blockCount = await base.GetBlockCountAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetSize(1)
					// The blockchain info does not change frequently.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2))
					.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

				// Save data in cache.
				Cache.Set(cacheKey, blockCount, cacheEntryOptions);
			}
			return blockCount;
		}

		public override async Task<PeerInfo[]> GetPeersInfoAsync()
		{
			string cacheKey = nameof(GetPeersInfoAsync);
			if (!Cache.TryGetValue(cacheKey, out PeerInfo[] peers))
			{
				peers = await base.GetPeersInfoAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetSize(2)
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(0.5));

				// Save data in cache.
				Cache.Set(cacheKey, peers, cacheEntryOptions);
			}
			return peers;
		}

		public override async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true)
		{
			string cacheKey = $"{nameof(GetMempoolEntryAsync)}:{txid}";
			if (!Cache.TryGetValue(cacheKey, out MempoolEntry mempoolEntry))
			{
				mempoolEntry = await base.GetMempoolEntryAsync(txid, throwIfNotFound).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetSize(20)
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2))
					.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

				// Save data in cache.
				Cache.Set(cacheKey, mempoolEntry, cacheEntryOptions);
			}
			return mempoolEntry;
		}

		public override async Task<uint256[]> GetRawMempoolAsync()
		{
			string cacheKey = nameof(GetRawMempoolAsync);
			if (!Cache.TryGetValue(cacheKey, out uint256[] mempool))
			{
				mempool = await base.GetRawMempoolAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetSize(20)
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2))
					.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

				// Save data in cache.
				Cache.Set(cacheKey, mempool, cacheEntryOptions);
			}
			return mempool;
		}

		public override GetTxOutResponse GetTxOut(uint256 txid, int index, bool includeMempool = true)
		{
			string cacheKey = $"{nameof(GetTxOut)}:{txid}:{index}:{includeMempool}";
			if (!Cache.TryGetValue(cacheKey, out GetTxOutResponse txout))
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
	}
}