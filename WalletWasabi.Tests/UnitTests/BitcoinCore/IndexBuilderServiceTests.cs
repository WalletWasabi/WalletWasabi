using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.BitcoinRpc.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

public class IndexBuilderServiceTests
{
	[Fact]
	public async Task SegwitTaprootUnsynchronizedBitcoinNodeAsync()
	{
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = 0,
				Blocks = 0,
				InitialBlockDownload = false
			}),
		};
		using var blockNotifier = new BlockNotifier(rpc);
		var indexer = new IndexBuilderService(rpc, blockNotifier, "filters.sqlite");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(1));
		//// Assert.False(indexer.IsRunning);     // <------------ ERROR: it should have stopped but there is a bug for RegTest
		// There is only starting filter
		Assert.True(indexer.GetLastFilter()?.Header.BlockHash.Equals(StartingFilters.GetStartingFilter(rpc.Network).Header.BlockHash));
	}

	[Fact]
	public async Task SegwitTaprootStalledBitcoinNodeAsync()
	{
		var called = 0;
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = 10_000,
					Blocks = 0,
					InitialBlockDownload = true
				});
			}
		};
		using var blockNotifier = new BlockNotifier(rpc);
		var indexer = new IndexBuilderService(rpc, blockNotifier, "filters.sqlite");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(2));
		Assert.True(indexer.IsRunning);  // It is still working
		// There is only starting filter
		Assert.True(indexer.GetLastFilter()?.Header.BlockHash.Equals(StartingFilters.GetStartingFilter(rpc.Network).Header.BlockHash));
		Assert.True(called > 1);
	}

	[Fact]
	public async Task SegwitTaprootSynchronizingBitcoinNodeAsync()
	{
		var called = 0;
		var blockchain = GenerateBlockchain().Take(10).ToArray();
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = (ulong)blockchain.Length,
					Blocks = (ulong)called - 1,
					InitialBlockDownload = true
				});
			},
			OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
			OnGetBlockFilterAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash).Filter)
		};
		using var blockNotifier = new BlockNotifier(rpc);
		var indexer = new IndexBuilderService(rpc, blockNotifier, "filters.txt");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(10));
		Assert.True(indexer.IsRunning);  // It is still working

		var lastFilter = indexer.GetLastFilter();
		Assert.Equal(9, (int)lastFilter!.Header.Height);
		Assert.True(called > 1);
	}

	[Fact]
	public async Task SegwitTaprootSynchronizedBitcoinNodeAsync()
	{
		var blockchain = GenerateBlockchain().Take(10).ToArray();
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = (ulong)blockchain.Length - 1,
				Blocks = (ulong)blockchain.Length - 1,
				InitialBlockDownload = false
			}),
			OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
			OnGetBlockFilterAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash).Filter)
		};
		using var blockNotifier = new BlockNotifier(rpc);
		var indexer = new IndexBuilderService(rpc, blockNotifier, "filters.txt");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(5));
		Assert.False(indexer.IsRunning);  // we are done

		var result = indexer.GetFilterLinesExcluding(blockchain[1].Hash, 100, out var found);
		Assert.True(found);
		Assert.Equal(9, result.bestHeight.Value);
		Assert.Equal(8, result.filters.Count());
	}

	[Fact]
	public async Task TaprootUnsynchronizedBitcoinNodeAsync()
	{
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = 0,
				Blocks = 0,
				InitialBlockDownload = false
			}),
		};
		using var blockNotifier = new BlockNotifier(rpc);
		var indexer = new IndexBuilderService(rpc, blockNotifier, "filters.sqlite");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(1));
		//// Assert.False(indexer.IsRunning);     // <------------ ERROR: it should have stopped but there is a bug for RegTest
		// There is only starting filter
		Assert.True(indexer.GetLastFilter()?.Header.BlockHash.Equals(StartingFilters.GetStartingFilter(rpc.Network).Header.BlockHash));
	}

	[Fact]
	public async Task TaprootStalledBitcoinNodeAsync()
	{
		var called = 0;
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = 10_000,
					Blocks = 0,
					InitialBlockDownload = true
				});
			}
		};
		using var blockNotifier = new BlockNotifier(rpc);
		var indexer = new IndexBuilderService(rpc, blockNotifier, "filters.sqlite");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(2));
		Assert.True(indexer.IsRunning);  // It is still working
		// There is only starting filter
		Assert.True(indexer.GetLastFilter()?.Header.BlockHash.Equals(StartingFilters.GetStartingFilter(rpc.Network).Header.BlockHash));
		Assert.True(called > 1);
	}

	[Fact]
	public async Task TaprootSynchronizingBitcoinNodeAsync()
	{
		var called = 0;
		var blockchain = GenerateBlockchain().Take(10).ToArray();
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = (ulong)blockchain.Length,
					Blocks = (ulong)called - 1,
					InitialBlockDownload = true
				});
			},
			OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
			OnGetBlockFilterAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash).Filter)
		};
		using var blockNotifier = new BlockNotifier(rpc);
		var indexer = new IndexBuilderService(rpc, blockNotifier, "filters.txt");

		indexer.Synchronize();

		await Task.Delay(TimeSpan.FromSeconds(10));
		Assert.True(indexer.IsRunning);  // It is still working

		var lastFilter = indexer.GetLastFilter();
		Assert.Equal(9, (int)lastFilter!.Header.Height);
		Assert.True(called > 1);
	}

	private IEnumerable<(uint256 Hash, BlockFilter Filter)> GenerateBlockchain() =>
		from height in Enumerable.Range(0, int.MaxValue).Select(x => (ulong)x)
		select (BlockHashFromHeight(height), new BlockFilter(GolombRiceFilter.Empty, uint256.Zero));

	private static uint256 BlockHashFromHeight(ulong height)
		=> Hashes.DoubleSHA256(BitConverter.GetBytes(height));
}
