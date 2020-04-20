using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Wallets
{
	/// <summary>
	/// SmartP2pBlockProvider is a blocks provider that can provides 
	/// blocks from multiple requesters.
	/// </summary>
	public class SmartBlockProvider : IBlockProvider
	{
		public SmartBlockProvider(IBlockProvider provider, IMemoryCache cache)
		{
			InnerBlockProvider = provider;
			Cache = cache;
		}
		
		private IBlockProvider InnerBlockProvider { get; }

		public IMemoryCache Cache { get; }

		private AsyncLock Lock { get; } = new AsyncLock();

		public Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancel)
		{
			lock (Lock)
			{
				string cacheKey = $"{nameof(SmartBlockProvider):nameof(GetBlockAsync)}:{blockHash}";
				if (!Cache.TryGetValue(cacheKey, out Task<Block> getBlockTask))
				{
					getBlockTask = InnerBlockProvider.GetBlockAsync(blockHash, cancel);

					var cacheEntryOptions = new MemoryCacheEntryOptions()
						.SetSize(10)
						// There is a block every 10 minutes in average so, keep in cache for 4 seconds.
						.SetAbsoluteExpiration(TimeSpan.FromSeconds(4));

					// Save data in cache.
					Cache.Set(cacheKey, getBlockTask, cacheEntryOptions);
				}
				return getBlockTask;
			}
		}
	}
}