using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using WalletWasabi.Legal;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class SmartBlockProviderTests
	{
		[Fact]
		public async void TestAsync()
		{
			var blocks = new Dictionary<uint256, Block>
			{
				[uint256.Zero] = Block.CreateBlock(Network.Main),
				[uint256.One] = Block.CreateBlock(Network.Main)
			};

			var source = new MockProvider();
			source.OnGetBlockAsync = async (hash, cancel) =>
			{
				await Task.Delay(TimeSpan.FromSeconds(0.5));
				return blocks[hash];
			};
			using var cache = new MemoryCache(new MemoryCacheOptions());
			var blockProvider = new SmartBlockProvider(source, cache);

			var b1 = blockProvider.GetBlockAsync(uint256.Zero, CancellationToken.None);
			var b2 = blockProvider.GetBlockAsync(uint256.One, CancellationToken.None);
			var b3 = blockProvider.GetBlockAsync(uint256.Zero, CancellationToken.None);

			await Task.WhenAll(b1, b2, b3);
			Assert.Equal(await b1, await b3);
			Assert.NotEqual(await b1, await b2);
		}

		private class MockProvider : IBlockProvider
		{
			public Func<uint256, CancellationToken, Task<Block>> OnGetBlockAsync { get; set; }

			public Task<Block> GetBlockAsync(uint256 hash, CancellationToken cancel)
			{
				return OnGetBlockAsync(hash, cancel);
			}
		}
	}
}
