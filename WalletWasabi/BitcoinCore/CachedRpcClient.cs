using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Threading.Tasks;

namespace WalletWasabi.BitcoinCore
{
	public class CachedRpcClient : RpcClientBase
	{
		private IMemoryCache _cache;

		public CachedRpcClient(RPCClient rpc, IMemoryCache cache)
			: base(rpc)
		{
			_cache = cache;
		}

		public override async Task<uint256> GetBestBlockHashAsync()
		{
			uint256 blockHash;
			string cacheKey = nameof(GetBestBlockHashAsync);
			if (!_cache.TryGetValue(cacheKey, out blockHash))
			{
				blockHash = await base.GetBestBlockHashAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					// The best hash doesn't change so often so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

				// Save data in cache.
				_cache.Set(cacheKey, blockHash, cacheEntryOptions);
			}
			return blockHash;
		}

		public override async Task<Block> GetBlockAsync(uint256 blockHash)
		{
			Block block;
			string cacheKey = $"{nameof(GetBlockAsync)}:{blockHash}";
			if (!_cache.TryGetValue(cacheKey, out block))
			{
				block = await base.GetBlockAsync(blockHash).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					// There is a block every 10 minutes in average so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

				// Save data in cache.
				_cache.Set(cacheKey, block, cacheEntryOptions);
			}
			return block;
		}

		public override async Task<Block> GetBlockAsync(uint blockHeight)
		{
			Block block;
			string cacheKey = $"{nameof(GetBlockAsync)}:{blockHeight}";
			if (!_cache.TryGetValue(cacheKey, out block))
			{
				block = await base.GetBlockAsync(blockHeight).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					// There is a block every 10 minutes in average so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

				// Save data in cache.
				_cache.Set(cacheKey, block, cacheEntryOptions);
			}
			return block;
		}

		public override async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash)
		{
			BlockHeader blockHeader;
			string cacheKey = $"{nameof(GetBlockHeaderAsync)}:{blockHash}";
			if (!_cache.TryGetValue(cacheKey, out blockHeader))
			{
				blockHeader = await base.GetBlockHeaderAsync(blockHash).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					// There is a block every 10 minutes in average so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

				// Save data in cache.
				_cache.Set(cacheKey, blockHeader, cacheEntryOptions);
			}
			return blockHeader;
		}

		public override async Task<BlockchainInfo> GetBlockchainInfoAsync()
		{
			BlockchainInfo blockchainInfo;
			string cacheKey = nameof(GetBlockchainInfoAsync);
			if (!_cache.TryGetValue(cacheKey, out blockchainInfo))
			{
				blockchainInfo = await  base.GetBlockchainInfoAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					// The blockchain info does not change frequently.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

				// Save data in cache.
				_cache.Set(cacheKey, blockchainInfo, cacheEntryOptions);
			}
			return blockchainInfo;
		}

		public override async Task<PeerInfo[]> GetPeersInfoAsync()
		{
			PeerInfo[] peers;
			string cacheKey = nameof(GetPeersInfoAsync);
			if (!_cache.TryGetValue(cacheKey, out peers))
			{
				peers = await base.GetPeersInfoAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(0.5));

				// Save data in cache.
				_cache.Set(cacheKey, peers, cacheEntryOptions);
			}
			return peers;
		}

		public override async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true)
		{
			MempoolEntry mempoolEntry;
			string cacheKey = $"{nameof(GetMempoolEntryAsync)}:{txid}";
			if (!_cache.TryGetValue(cacheKey, out mempoolEntry))
			{
				mempoolEntry = await base.GetMempoolEntryAsync(txid, throwIfNotFound).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

				// Save data in cache.
				_cache.Set(cacheKey, mempoolEntry, cacheEntryOptions);
			}
			return mempoolEntry;
		}

		public override async Task<uint256[]> GetRawMempoolAsync()
		{
			uint256[] mempool;
			string cacheKey = nameof(GetRawMempoolAsync);
			if (!_cache.TryGetValue(cacheKey, out mempool))
			{
				mempool = await base.GetRawMempoolAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

				// Save data in cache.
				_cache.Set(cacheKey, mempool, cacheEntryOptions);
			}
			return mempool;
		}

		public override GetTxOutResponse GetTxOut(uint256 txid, int index, bool includeMempool = true)
		{
			GetTxOutResponse txout;
			string cacheKey = $"{nameof(GetTxOut)}:{txid}:{index}:{includeMempool}";
			if (!_cache.TryGetValue(cacheKey, out txout))
			{
				txout = base.GetTxOut(txid, index, includeMempool);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

				// Save data in cache.
				_cache.Set(cacheKey, txout, cacheEntryOptions);
			}
			return txout;
		}
	}
}