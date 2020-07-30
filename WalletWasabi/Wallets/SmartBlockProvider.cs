using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Wallets
{
	/// <summary>
	/// SmartP2pBlockProvider is a blocks provider that can provide
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
				string cacheKey = $"{nameof(SmartBlockProvider)}:{nameof(GetBlockAsync)}:{blockHash}";
				return Cache.GetOrCreate(cacheKey, entry =>
				{
					entry.SetSize(10);
					entry.SetSlidingExpiration(TimeSpan.FromSeconds(4));
					entry.RegisterPostEvictionCallback(callback: EvictionCallback, state: this);

					return InnerBlockProvider.GetBlockAsync(blockHash, cancel);
				});
			}
		}

		private void EvictionCallback(object key, object value, EvictionReason reason, object state)
		{
			var task = value as Task<Block>;
			if (task.IsCompleted)
			{
				task?.Dispose();
			}
		}
	}
}
