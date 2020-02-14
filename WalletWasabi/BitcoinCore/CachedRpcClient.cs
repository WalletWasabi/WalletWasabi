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
		public CachedRpcClient(IRPCClient rpc, IMemoryCache cache)
		: base(rpc)
		{
			Cache = cache;
		}
		
		public IMemoryCache Cache { get; }

		public override async Task<uint256> GetBestBlockHashAsync()
		{
			uint256 blockHash;
			string cacheKey = nameof(GetBestBlockHashAsync);
			if (!Cache.TryGetValue(cacheKey, out blockHash))
			{
				blockHash = await base.GetBestBlockHashAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					// The best hash doesn't change so often so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

				// Save data in cache.
				Cache.Set(cacheKey, blockHash, cacheEntryOptions);
			}
			return blockHash;
		}

		public override async Task<Block> GetBlockAsync(uint256 blockHash)
		{
			Block block;
			string cacheKey = $"{nameof(GetBlockAsync)}:{blockHash}";
			if (!Cache.TryGetValue(cacheKey, out block))
			{
				block = await base.GetBlockAsync(blockHash).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					// There is a block every 10 minutes in average so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

				// Save data in cache.
				Cache.Set(cacheKey, block, cacheEntryOptions);
			}
			return block;
		}

		public override async Task<Block> GetBlockAsync(uint blockHeight)
		{
			Block block;
			string cacheKey = $"{nameof(GetBlockAsync)}:{blockHeight}";
			if (!Cache.TryGetValue(cacheKey, out block))
			{
				block = await base.GetBlockAsync(blockHeight).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					// There is a block every 10 minutes in average so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

				// Save data in cache.
				Cache.Set(cacheKey, block, cacheEntryOptions);
			}
			return block;
		}

		public override async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash)
		{
			BlockHeader blockHeader;
			string cacheKey = $"{nameof(GetBlockHeaderAsync)}:{blockHash}";
			if (!Cache.TryGetValue(cacheKey, out blockHeader))
			{
				blockHeader = await base.GetBlockHeaderAsync(blockHash).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					// There is a block every 10 minutes in average so, keep in cache for 4 seconds.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

				// Save data in cache.
				Cache.Set(cacheKey, blockHeader, cacheEntryOptions);
			}
			return blockHeader;
		}

/*
		public override async Task<BlockchainInfo> GetBlockchainInfoAsync()
		{
			BlockchainInfo blockchainInfo;
			string cacheKey = nameof(GetBlockchainInfoAsync);
			if (!Cache.TryGetValue(cacheKey, out blockchainInfo))
			{
				blockchainInfo = await base.GetBlockchainInfoAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					// The blockchain info does not change frequently.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

				// Save data in cache.
				Cache.Set(cacheKey, blockchainInfo, cacheEntryOptions);
			}
			return blockchainInfo;
		}
*/

		public override async Task<int> GetBlockCountAsync()
		{
			int blockCount;
			string cacheKey = nameof(GetBlockCountAsync);
			if (!Cache.TryGetValue(cacheKey, out blockCount))
			{
				blockCount = await base.GetBlockCountAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					// The blockchain info does not change frequently.
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

				// Save data in cache.
				Cache.Set(cacheKey, blockCount, cacheEntryOptions);
			}
			return blockCount;
		}

		public override async Task<PeerInfo[]> GetPeersInfoAsync()
		{
			PeerInfo[] peers;
			string cacheKey = nameof(GetPeersInfoAsync);
			if (!Cache.TryGetValue(cacheKey, out peers))
			{
				peers = await base.GetPeersInfoAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(0.5));

				// Save data in cache.
				Cache.Set(cacheKey, peers, cacheEntryOptions);
			}
			return peers;
		}

		public override async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true)
		{
			MempoolEntry mempoolEntry;
			string cacheKey = $"{nameof(GetMempoolEntryAsync)}:{txid}";
			if (!Cache.TryGetValue(cacheKey, out mempoolEntry))
			{
				mempoolEntry = await base.GetMempoolEntryAsync(txid, throwIfNotFound).ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

				// Save data in cache.
				Cache.Set(cacheKey, mempoolEntry, cacheEntryOptions);
			}
			return mempoolEntry;
		}

		public override async Task<uint256[]> GetRawMempoolAsync()
		{
			uint256[] mempool;
			string cacheKey = nameof(GetRawMempoolAsync);
			if (!Cache.TryGetValue(cacheKey, out mempool))
			{
				mempool = await base.GetRawMempoolAsync().ConfigureAwait(false);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

				// Save data in cache.
				Cache.Set(cacheKey, mempool, cacheEntryOptions);
			}
			return mempool;
		}

		public override GetTxOutResponse GetTxOut(uint256 txid, int index, bool includeMempool = true)
		{
			GetTxOutResponse txout;
			string cacheKey = $"{nameof(GetTxOut)}:{txid}:{index}:{includeMempool}";
			if (!Cache.TryGetValue(cacheKey, out txout))
			{
				txout = base.GetTxOut(txid, index, includeMempool);

				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

				// Save data in cache.
				Cache.Set(cacheKey, txout, cacheEntryOptions);
			}
			return txout;
		}

		public override async Task<uint256[]> GenerateAsync(int blockCount)
		{
			Cache.Remove(nameof(GetBlockchainInfoAsync));
			Cache.Remove(nameof(GetRawMempoolAsync));
			Cache.Remove(nameof(GetBlockCountAsync));
			Cache.Remove(nameof(GetBestBlockHashAsync));
			return await base.GenerateAsync(blockCount).ConfigureAwait(false);
		}
	}
}