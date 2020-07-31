using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Wallets
{
	/// <summary>
	/// SmartP2pBlockProvider is a block provider that can provide
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

		public async Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancel)
		{
			string cacheKey = $"{nameof(SmartBlockProvider)}:{nameof(GetBlockAsync)}:{blockHash}";
			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				entry =>
				{
					entry.SetSize(10);
					entry.SetSlidingExpiration(TimeSpan.FromSeconds(4));

					return InnerBlockProvider.GetBlockAsync(blockHash, cancel);
				}).ConfigureAwait(false);
		}
	}
}
